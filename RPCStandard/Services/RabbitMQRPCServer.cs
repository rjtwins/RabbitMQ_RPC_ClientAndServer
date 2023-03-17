using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace RPC.Services
{
    public class RabbitMQRPCServer : RabbitMQInterface, IRabbitMQRPCServer
    {
        private int _queuExpireTime = (3 * 24 * 60 * 60 * 1000);
        public void Setup(int? QueuExpireTime = null)
        {
            base.Setup(0, 1);

            if (QueuExpireTime != null)
                _queuExpireTime = QueuExpireTime.Value;

            _channel.BasicQos(0, 1, false);
        }

        public void Setup(string rabbitMQUri, int? QueuExpireTime = null)
        {
            base.Setup(rabbitMQUri, 0, 1);

            if (QueuExpireTime != null)
                _queuExpireTime = QueuExpireTime.Value;

            _channel.BasicQos(0, 1, false);
        }

        public void Subscribe(Delegate del, string name)
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

            EventingBasicConsumer consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (object sender, BasicDeliverEventArgs args2) =>
            {



                if (_channel == null)
                {
                    _channel.BasicNack(args2.DeliveryTag, multiple: false, true);
                }

                string serializedResponse = string.Empty;

                try
                {
                    string message = Encoding.UTF8.GetString(args2.Body.ToArray());

                    Debug.WriteLine($"Server Queue {args2.RoutingKey} received {message} correlationId {args2.BasicProperties.CorrelationId}.");

                    var arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(message, _settings).ToList();

                    object response = del.DynamicInvoke(arguments.ToArray());
                    serializedResponse = Newtonsoft.Json.JsonConvert.SerializeObject(response, _settings);
                }
                catch (Exception)
                {

                }
                finally
                {
                    Respond(serializedResponse, args2.BasicProperties.ReplyTo, args2.BasicProperties.CorrelationId);

                    _channel.BasicAck(args2.DeliveryTag, multiple: false);
                }
            };

            _channel.BasicConsume(queue, false, consumer);
        }

        private void Respond(string message, string replyTo, string correlationId)
        {
            IBasicProperties props = _channel.CreateBasicProperties();
            props.CorrelationId = correlationId;

            _channel.BasicPublish("", replyTo, props, Encoding.UTF8.GetBytes(message));

            Debug.WriteLine($"Server responded to {replyTo} message {message} correlationId {correlationId}.");

        }
    }
}
