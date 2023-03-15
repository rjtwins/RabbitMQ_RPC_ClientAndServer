using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace RPC.Services
{
    public class RabbitMQRPCServer : RabbitMQInterface, IRabbitMQRPCServer
    {
        public bool Setup()
        {
            if (!base.Setup(0, 1))
                return false;

            if (_channel == null)
                return false;

            _channel.BasicQos(0, 1, false);

            return true;
        }

        public bool Setup(string rabbitMQUri)
        {
            if (!base.Setup(rabbitMQUri, 0, 1))
                return false;

            if (_channel == null)
                return false;

            _channel.BasicQos(0, 1, false);

            return true;
        }

        public void Subscribe(Delegate del, string name)
        {
            if (del == null)
                return;

            if (_channel == null)
                return;

            //if (!del.Method.ReturnType.IsPrimitive && del.Method.ReturnType.GetInterface("RPC.Serialization.IJsonSerializable") == null)
            //    return;

            //if(del.Method.GetParameters().Any(x => !x.ParameterType.IsPrimitive && x.ParameterType.GetInterface("RPC.Serialization.IJsonSerializable") == null))
            //    return;

            string queue = name;
            Dictionary<string, object> args = new Dictionary<string, object>();
            //36 hours to span a weekend.
            args["x-expires"] = (3 * 24 * 60 * 60 * 1000);

            _channel.QueueDeclare(queue, true, false, false, args);

            EventingBasicConsumer consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (object? sender, BasicDeliverEventArgs args) =>
            {
                if (_channel == null)
                    return;

                string message = Encoding.UTF8.GetString(args.Body.ToArray());
                var arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(message, new JsonInt32Converter()).ToList();
                
                object? response = del.DynamicInvoke(arguments.ToArray());
                string serializedResponse = JsonSerializer.Serialize(response);

                Respond(serializedResponse, args.BasicProperties.ReplyTo, args.BasicProperties.CorrelationId);

                _channel.BasicAck(args.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(queue, false, consumer);
        }

        //public void Subscribe(MethodInfo method, string? name = null, object? instance = null)
        //{
        //    if (method == null)
        //        return;

        //    if (instance == null && !method.IsStatic)
        //        throw new ArgumentException("When providing a null instance, the method must be static.");

        //    if (_channel == null)
        //        return;

        //    //if (!method.ReturnType.IsPrimitive && method.ReturnType.GetInterface("RPC.Serialization.IJsonSerializable") == null)
        //    //    return;

        //    string queue = string.IsNullOrEmpty(name) ? method.Name : name;
        //    Dictionary<string, object> args = new Dictionary<string, object>();

        //    //36 hours to span a weekend.
        //    args["x-expires"] = (3 * 24 * 60 * 60 * 1000);

        //    _channel.QueueDeclare(queue, true, false, false, args);

        //    EventingBasicConsumer consumer = new EventingBasicConsumer(_channel);

        //    consumer.Received += (object? sender, BasicDeliverEventArgs args) =>
        //    {
        //        if (_channel == null)
        //            return;

        //        string message = Encoding.UTF8.GetString(args.Body.ToArray());
        //        var arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(message, new JsonInt32Converter()).ToList();

        //        if (instance == null && !method.IsStatic)
        //            return;

        //        object? response = method.Invoke(instance, arguments.ToArray());
        //        string serializedResponse = JsonSerializer.Serialize(response);

        //        Respond(serializedResponse, args.BasicProperties.ReplyTo, args.BasicProperties.CorrelationId);

        //        _channel.BasicAck(args.DeliveryTag, multiple: false);
        //    };

        //    _channel.BasicConsume(queue, false, consumer);
        //}

        private void Respond(string message, string replyTo, string correlationId)
        {
            if (_channel == null)
                return;

            IBasicProperties props = _channel.CreateBasicProperties();
            props.CorrelationId = correlationId;

            _channel.BasicPublish("", replyTo, props, Encoding.UTF8.GetBytes(message));
        }
    }
}
