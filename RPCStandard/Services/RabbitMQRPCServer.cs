using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

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

            _basicConsumer.Received += (object sender, BasicDeliverEventArgs deliveryArgs) =>
            {
                if (_channel == null)
                {
                    _channel.BasicNack(deliveryArgs.DeliveryTag, multiple: false, true);
                }

                string message = Encoding.UTF8.GetString(deliveryArgs.Body.ToArray());
                string correlationId = deliveryArgs.BasicProperties.CorrelationId;
                string replyTo = deliveryArgs.BasicProperties.ReplyTo;
                string from = deliveryArgs.RoutingKey;

                if (!async)
                    ProcessCall(message, correlationId, replyTo, from, del);
                else
                    Task.Factory.StartNew(() => ProcessCall(message, correlationId, replyTo, from, del));

                _channel.BasicAck(deliveryArgs.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(queue, false, _basicConsumer);
        }

        private void ProcessCall(string message, string correlationId, string replyTo, string from, Delegate del)
        {
            string serializedResponse = string.Empty;
            try
            {
#if DEBUG
                Debug.WriteLine($"Server Queue {from} received {message} correlationId {correlationId}.");
#endif
                var arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(message, _settings).ToList();

                object response = del.DynamicInvoke(arguments.ToArray());
                serializedResponse = Newtonsoft.Json.JsonConvert.SerializeObject(response, _settings);

            }
            catch (Exception ex)
            {
                serializedResponse = $"Remote exceptoin: {ex.Message}, {ex.StackTrace}";
            }
            finally
            {
                Respond(serializedResponse, replyTo, correlationId);
            }
        }

        private void Respond(string message, string replyTo, string correlationId)
        {
            IBasicProperties props = _channel.CreateBasicProperties();
            props.CorrelationId = correlationId;

            _channel.BasicPublish("", replyTo, props, Encoding.UTF8.GetBytes(message));
#if DEBUG
            Debug.WriteLine($"Server responded to {replyTo} message {message} correlationId {correlationId}.");
#endif
        }
    }
}
