using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using RPC.Models;

namespace RPC.Services
{
    public sealed class RabbitMQRPCServer : RabbitMQInterface, IRabbitMQRPCServer
    {
        private int _queuExpireTime = (3 * 24 * 60 * 60 * 1000);

        /// <inheritdoc/>
        public event EventHandler<RabbitMQRPCMessageEventArgs> MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<RabbitMQRPCMessageEventArgs> Responded;

        private readonly MiddlewareProvider _receiverMiddleware = new MiddlewareProvider();
        private readonly MiddlewareProvider _responderMiddleware = new MiddlewareProvider();

        /// <inheritdoc/>
        public void Setup(int? QueuExpireTime = null)
        {
            base.Setup(0, 1);

            if (QueuExpireTime != null)
                _queuExpireTime = QueuExpireTime.Value;

            _channel.BasicQos(0, 1, false);
        }

        /// <inheritdoc/>
        public void Setup(string rabbitMQUri, int? QueuExpireTime = null)
        {
            base.Setup(rabbitMQUri, 0, 1);

            if (QueuExpireTime != null)
                _queuExpireTime = QueuExpireTime.Value;

            _channel.BasicQos(0, 1, false);
        }

        /// <inheritdoc/>
        public void ReceiverUse(Func<RabbitMQRPCMessage, Func<Task>, Task> function)
        {
            _receiverMiddleware.Use(function);
        }

        /// <inheritdoc/>
        public void ResponderUse(Func<RabbitMQRPCMessage, Func<Task>, Task> function)
        {
            _responderMiddleware.Use(function);
        }

        /// <inheritdoc/>
        public void Subscribe(Delegate del, string name, bool async = false)
        {
            if (del == null)
                throw new ArgumentNullException(nameof(del));

            if (_channel == null)
                throw new ArgumentNullException(nameof(name));

            //To support overloading methods.
            name += "_" + string.Join("_", del.Method.GetParameters().ToList().Select(x => x.ParameterType.Name));
            name += "_" + del.Method.ReturnParameter.ParameterType.Name;

            string queue = name;
            Dictionary<string, object> args = new Dictionary<string, object>();
            //36 hours to span a weekend.
            args["x-expires"] = _queuExpireTime;

            _channel.QueueDeclare(queue, true, false, false, args);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (object sender, BasicDeliverEventArgs args2) =>
            {
                Consumer_Received(sender, args2, async, del);
            };

            _channel.BasicConsume(queue, false, consumer);
        }

        private void Consumer_Received(object sender, BasicDeliverEventArgs args, bool async, Delegate del)
        {
            //Context
            RabbitMQRPCMessage context = new RabbitMQRPCMessage()
            {
                Message = Encoding.UTF8.GetString(args.Body.ToArray()),
                CorrelationId = args.BasicProperties.CorrelationId,
                ReplyTo = args.BasicProperties.ReplyTo,
                Queue = args.RoutingKey,
                DeliveryTag = args.DeliveryTag
            };

            //Clone middleware stack.
            var middleware = (MiddlewareProvider)_receiverMiddleware.Clone();

            //Setup response variables.
            var response = string.Empty;
            Task<string> responseTask = null;

            //Add final task to middleware stack.
            middleware.Use((message, next) =>
            {
#if DEBUG
                Debug.WriteLine($"SERVER: Queue {context.Queue} received message correlationId {context.CorrelationId}.");
#endif
                MessageReceived?.Invoke(this, new RabbitMQRPCMessageEventArgs() { Message = message.Message, CorrelationId = message.CorrelationId, Queue = message.Queue, ReplyTo = message.ReplyTo });

                if (!async)
                    response = ProcessCall(message.Message, message.CorrelationId, message.ReplyTo, message.Queue, del);
                else
                    responseTask = Task.Factory.StartNew<string>(() => ProcessCall(message.Message, message.CorrelationId, message.ReplyTo, message.Queue, del));

                _channel.BasicAck(args.DeliveryTag, multiple: false);

                return Task.CompletedTask;
            });

            //Run middleware stack and actual task.
            middleware.Run(context).Wait();

            //Handle result
            if (async)
            {
                responseTask.ContinueWith((Task<string> task) =>
                {
                    Respond(task.Result, context.ReplyTo, context.CorrelationId);
                });
                return;
            }
            Respond(response, context.ReplyTo, context.CorrelationId);
        }

        private string ProcessCall(string message, string correlationId, string replyTo, string from, Delegate del)
        {
            string serializedResponse = string.Empty;
            try
            {
                var arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(message, _settings).ToList();

                object response = del.DynamicInvoke(arguments.ToArray());
                serializedResponse = Newtonsoft.Json.JsonConvert.SerializeObject(response, _settings);
            }
            catch (Exception ex)
            {
                serializedResponse = $"Remote exception: {ex.Message}, {ex.StackTrace}";
            }
            return serializedResponse;
        }

        private void Respond(string messageText, string replyTo, string correlationId)
        {
            //Context
            RabbitMQRPCMessage context = new RabbitMQRPCMessage()
            {
                Message = messageText,
                CorrelationId = correlationId,
                ReplyTo = string.Empty,
                Queue = replyTo,
                DeliveryTag = 0
            };

            var middleware = (MiddlewareProvider)_responderMiddleware.Clone();

            //Add final task to middleware stack.
            middleware.Use((message, next) =>
            {
                //Generate publish properties
                IBasicProperties props = _channel.CreateBasicProperties();
                props.CorrelationId = message.CorrelationId;

                _channel.BasicPublish("", message.Queue, props, Encoding.UTF8.GetBytes(message.Message));
#if DEBUG
                Debug.WriteLine($"SERVER: Responded to {message.Queue} message correlationId {message.CorrelationId}.");
#endif
                Responded?.Invoke(this, new RabbitMQRPCMessageEventArgs() { Message = message.Message, CorrelationId = message.CorrelationId, Queue = message.Queue, ReplyTo = message.ReplyTo });

                return Task.CompletedTask;
            });

            //Run middleware stack and actual task.
            middleware.Run(context).Wait();
        }
    }
}
