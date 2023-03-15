using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RPC.Services
{
    public abstract class RabbitMQInterface
    {
        private const int RETRY_TIMES = 3;

        protected IConnection? _connection { get; set; }
        protected IModel? _channel { get; set; }
        protected EventingBasicConsumer? _basicConsumer { get; set; }
        private string _rabbitMQUri { get; set; } = string.Empty;
        private bool setup { get; set; } = false;
        private int _prefetchSize, _prefetchCount = 0;

        protected bool Setup(int prefetchSize = 0, int prefetchCount = 0)
        {
            if (string.IsNullOrEmpty(_rabbitMQUri))
                return false;

            return Setup(_rabbitMQUri, prefetchSize, prefetchCount);
        }

        /// <inheritdoc/>
        protected bool Setup(string rabbitMQUri, int prefetchSize = 0, int prefetchCount = 0)
        {
            if (setup)
                throw new InvalidOperationException("RabbitMQInterface was allready setup, use Reset() to reset the instance.");

            if (string.IsNullOrEmpty(_rabbitMQUri))
                throw new InvalidOperationException("Provided connection uri was empty.");

            _rabbitMQUri = rabbitMQUri;
            _prefetchSize = prefetchSize;
            _prefetchCount = prefetchCount;

            if (!CreateConnection())
                return false;

            if (!CreateChannel())
                return false;

            if (!CreateConsumer())
                return false;

            setup = true;

            return true;
        }

        public virtual void Reset()
        {
            _basicConsumer = null;
            _rabbitMQUri = string.Empty;
            _prefetchSize = 0;
            _prefetchCount = 0;
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();

            setup = false;
        }

        public void SetRabbitMQURI(string uri)
        {
            _rabbitMQUri = uri;
        }

        /// <summary>
        /// Create connection with recursive retry pattern.
        /// </summary>
        private bool CreateConnection(int retry = 0)
        {
            try
            {
                ConnectionFactory factory = new ConnectionFactory();
                factory.AutomaticRecoveryEnabled = true;
                factory.TopologyRecoveryEnabled = true;

                factory.Uri = new Uri(_rabbitMQUri);
                _connection = factory.CreateConnection();

                return true;
            }
            catch (Exception)
            {
                if (RETRY_TIMES <= retry)
                {
                    return false;
                }

                Thread.Sleep(5000 * retry);
                return CreateConnection(retry + 1);
            }
        }

        /// <summary>
        /// Create model.
        /// </summary>
        private bool CreateChannel()
        {
            if (_connection == null || !_connection.IsOpen)
                return false;

            try
            {
                _channel = _connection.CreateModel();
                _channel.BasicQos(prefetchSize: (uint)_prefetchSize, prefetchCount: (ushort)_prefetchCount, global: false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Create the consumer.
        /// </summary>
        /// <returns></returns>
        private bool CreateConsumer()
        {
            if (_channel == null || !_channel.IsOpen)
                return false;

            try
            {
                _basicConsumer = new EventingBasicConsumer(_channel);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}