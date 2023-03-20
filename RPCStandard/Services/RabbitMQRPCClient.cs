using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RPC.Services
{
    public class RabbitMQRPCClient : RabbitMQInterface, IRabbitMQRPCClient
    {
        private string _replyQueue { set; get; } = string.Empty;
        private ConcurrentDictionary<string, TaskCompletionSource<string>> _responseDict { set; get; } = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

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
                string message = Encoding.UTF8.GetString(args.Body.ToArray());
                if (!_responseDict.TryGetValue(args.BasicProperties.CorrelationId, out TaskCompletionSource<string> result))
                    return;

                if (result == null)
                    return;

#if DEBUG
                Debug.WriteLine($"Reply Queue {args.RoutingKey} received {message} correlationId {args.BasicProperties.CorrelationId}.");
#endif

                result.SetResult(message);
            };

            _channel.BasicConsume(_replyQueue, true, _basicConsumer);
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

            IBasicProperties props = _channel.CreateBasicProperties();
            props.CorrelationId = Guid.NewGuid().ToString();
            props.ReplyTo = _replyQueue;
            var taskCompletionSource = new TaskCompletionSource<string>();
            _responseDict.TryAdd(props.CorrelationId, taskCompletionSource);

            _channel.BasicPublish("", queue, props, Encoding.UTF8.GetBytes(body));

#if DEBUG
            Debug.WriteLine($"Message sent to {queue} message {body} replyTo {_replyQueue} correlationId {props.CorrelationId}");
#endif

            var taskResult = await taskCompletionSource.Task;

            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(taskCompletionSource.Task.Result);
            return result;
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
    }
}
