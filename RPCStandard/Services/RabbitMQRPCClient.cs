using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RPC.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Services
{
    public sealed class RabbitMQRPCClient : RabbitMQInterface, IRabbitMQRPCClient
    {
        private string _replyQueue { set; get; } = string.Empty;
        private ConcurrentDictionary<string, string> _responseDict { set; get; } = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, SemaphoreSlim> _semaphoreDict { set; get; } = new ConcurrentDictionary<string, SemaphoreSlim>();


        public event EventHandler<RabbitMQRPCMessageEventArgs> MessageSend;
        public event EventHandler<RabbitMQRPCMessageEventArgs> ReplyReceived;

        private readonly MiddlewareProvider<RabbitMQRPCMessage, string> _callerMiddleware = new MiddlewareProvider<RabbitMQRPCMessage, string>();

        /// <inheritdoc/>
        public void Setup()
        {
            base.Setup(0, 1);

            _channel.BasicQos(0, 1, false);

            _replyQueue = _channel.QueueDeclare("", false, true, true).QueueName;

            SetupReplyQueue();
        }

        /// <inheritdoc/>
        public void Setup(string rabbitMQUri)
        {
            base.Setup(rabbitMQUri, 0, 1);

            _channel.BasicQos(0, 1, false);

            _replyQueue = _channel.QueueDeclare("", false, true, true).QueueName;

            SetupReplyQueue();
        }

        private void SetupReplyQueue()
        {
            _basicConsumer.Received += (object sender, BasicDeliverEventArgs args) =>
            {
                //Context
                RabbitMQRPCMessage message = new RabbitMQRPCMessage()
                {
                    Message = Encoding.UTF8.GetString(args.Body.ToArray()),
                    CorrelationId = args.BasicProperties.CorrelationId,
                    ReplyTo = args.BasicProperties.ReplyTo,
                    Queue = args.BasicProperties.ReplyTo,
                    DeliveryTag = args.DeliveryTag
                };

                ReplyReceived?.Invoke(this, new RabbitMQRPCMessageEventArgs() { Message = message.Message, CorrelationId = message.CorrelationId, Queue = message.Queue, ReplyTo = message.ReplyTo });

                if (!_semaphoreDict.TryRemove(message.CorrelationId, out SemaphoreSlim semaphore))
                    return;
#if DEBUG
                Debug.WriteLine($"CLIENT: Reply Queue {args.RoutingKey} received message correlationId {args.BasicProperties.CorrelationId}.");
#endif
                _responseDict.TryAdd(message.CorrelationId, message.Message);
                semaphore.Release();
            };

            _channel.BasicConsume(_replyQueue, true, _basicConsumer);
        }

        /// <inheritdoc/>
        public void CallerUse(Func<RabbitMQRPCMessage, Func<Task<string>>, Task<string>> function)
        {
            _callerMiddleware.Use(function);
        }

        /// <inheritdoc/>
        public void Call(string alias, params object[] arguments)
        {
            Call<Models.Void>(alias, arguments);
        }

        /// <inheritdoc/>
        public void Call(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            Call(alias, arguments);
        }
        /// <inheritdoc/>
        public T Call<T>(string alias, params object[] arguments)
        {
            Task<T> task = CallAsync<T>(alias, arguments);
            task.Wait();

            return task.Result;
        }

        /// <inheritdoc/>
        public T Call<T>(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            Task<T> task = CallAsync<T>(alias, arguments);
            task.Wait();

            return task.Result;
        }

        /// <inheritdoc/>
        public async Task<T> CallAsync<T>(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            return await CallAsync<T>(alias, arguments);
        }

        /// <inheritdoc/>
        public async Task<T> CallAsync<T>(string alias, params object[] arguments)
        {
            if (_channel == null)
                throw new ArgumentNullException($"While calling {alias} RabbitMQ channel was null.");

            if(!_channel.IsOpen)
                throw new InvalidOperationException($"While calling {alias} RabbitMQ channel was closed.");

            string body = Newtonsoft.Json.JsonConvert.SerializeObject(arguments, _settings);
            string queue = alias;
            queue += "_" + string.Join("_", arguments.ToList().Select(x => x.GetType().Name));
            queue += "_" + typeof(T).Name;

            //Context
            RabbitMQRPCMessage context = new RabbitMQRPCMessage()
            {
                Message = body,
                CorrelationId = Guid.NewGuid().ToString(),
                ReplyTo = _replyQueue,
                Queue = queue,
                DeliveryTag = 0
            };

            //Clone middleware stack.
            var middleware = (MiddlewareProvider<RabbitMQRPCMessage, string>)_callerMiddleware.Clone();

            //Add final task to middleware.
            middleware.Use(async (message, next) =>
            {
                IBasicProperties props = _channel.CreateBasicProperties();
                props.CorrelationId = message.CorrelationId;
                props.ReplyTo = message.ReplyTo;

                SemaphoreSlim semaphore = new SemaphoreSlim(0);
                _semaphoreDict.TryAdd(message.CorrelationId, semaphore);

                _channel.BasicPublish("", message.Queue, props, Encoding.UTF8.GetBytes(message.Message));

                MessageSend?.Invoke(this, new RabbitMQRPCMessageEventArgs() { Message = message.Message, CorrelationId = message.CorrelationId, Queue = message.Queue, ReplyTo = message.ReplyTo });

#if DEBUG
                Debug.WriteLine($"CLIENT: Message sent to {message.Queue} replyTo {message.ReplyTo} correlationId {props.CorrelationId}");
#endif

                await Task.Factory.StartNew(() => semaphore.Wait());

#if DEBUG
                Debug.WriteLine($"CLIENT: Message semaphore for {props.CorrelationId} released.");
#endif
                _responseDict.TryRemove(message.CorrelationId, out var responseString);
                return responseString;
            });

            //Run middleware.
            string response = await middleware.Run(context);

            //Deserialize result.
            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response);

            //Return result.
            return result;
        }
    }
}
