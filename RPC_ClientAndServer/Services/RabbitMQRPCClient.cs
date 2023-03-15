using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace RPC.Services
{
    public class RabbitMQRPCClient : RabbitMQInterface, IRabbitMQRPCClient
    {
        private string _replyQueue = string.Empty;
        private ConcurrentDictionary<string, TaskCompletionSource<string>> _responseDict = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        public bool Setup()
        {
            if (!base.Setup(0, 1))
                return false;

            if (_channel == null)
                return false;

            _channel.BasicQos(0, 1, false);

            _replyQueue = _channel.QueueDeclare("", false, true, true).QueueName;

            if (!SetupReplyQueue())
                return false;

            return true;
        }

        public bool Setup(string rabbitMQUri)
        {
            if (!base.Setup(rabbitMQUri, 0, 1))
                return false;

            if (_channel == null)
                return false;

            _channel.BasicQos(0, 1, false);

            _replyQueue = _channel.QueueDeclare("", false, true, true).QueueName;

            if (!SetupReplyQueue())
                return false;

            return true;
        }

        private bool SetupReplyQueue()
        {
            if (_basicConsumer == null)
                return false;

            _basicConsumer.Received += (object? sender, BasicDeliverEventArgs args) =>
            {
                string message = Encoding.UTF8.GetString(args.Body.ToArray());
                if (!_responseDict.TryGetValue(args.BasicProperties.CorrelationId, out TaskCompletionSource<string>? result))
                    return;

                if (result == null)
                    return;

                if (!result.TrySetResult(message))
                    return;
            };

            _channel.BasicConsume(_replyQueue, true, _basicConsumer);

            return true;
        }

        public T Call<T>(string alias, params object[] arguments)
        {
            Task<T> task = CallAsync<T>(alias, arguments);
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Calls RPCServer method with the name of the calling method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public T Call<T>(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            Task<T> task = CallAsync<T>(alias, arguments);
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Async call RPCServer method with the name of the calling method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public async Task<T> CallAsync<T>(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            return await CallAsync<T>(alias, arguments);
        }

        public async Task<T> CallAsync<T>(string alias, params object[] arguments)
        {
            if (_channel == null)
                return default(T);

            string body = JsonSerializer.Serialize(arguments);
            string queue = alias;

            IBasicProperties props = _channel.CreateBasicProperties();
            props.CorrelationId = Guid.NewGuid().ToString();
            props.ReplyTo = _replyQueue;
            var taskCompletionSource = new TaskCompletionSource<string>();
            _responseDict.TryAdd(props.CorrelationId, taskCompletionSource);

            _channel.BasicPublish("", queue, props, Encoding.UTF8.GetBytes(body));

            string resultString = await taskCompletionSource.Task;

            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(resultString);

            return result;
        }

        public void Call(string alias, params object[] arguments)
        {
            Call<object>(alias, arguments);
        }

        public void Call(params object[] arguments)
        {
            var alias = (new System.Diagnostics.StackTrace()?.GetFrame(1)?.GetMethod() as MethodInfo)?.Name;
            if (alias == null)
                throw new InvalidOperationException("Calling methods name could not be resolved.");

            Call(alias, arguments);
        }
    }
}
