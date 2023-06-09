﻿using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;

namespace RPC.Services
{
    public abstract class RabbitMQInterface
    {
        private const int RETRY_TIMES = 3;

        protected IConnection _connection { get; set; }
        protected IModel _channel { get; set; }
        protected EventingBasicConsumer _basicConsumer { get; set; }
        private string _rabbitMQUri { get; set; } = string.Empty;
        private bool setup { get; set; } = false;
        private int _prefetchSize, _prefetchCount = 0;
        protected JsonSerializerSettings _settings { get; }

        public RabbitMQInterface()
        {
            _settings = new JsonSerializerSettings();
            _settings.TypeNameHandling = TypeNameHandling.All;
            _settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            _settings.Converters.Add(new JsonInt32Converter());
        }

        protected void Setup(int prefetchSize = 0, int prefetchCount = 0)
        {
            if (string.IsNullOrEmpty(_rabbitMQUri))
                throw new InvalidOperationException("RabbitMQ connection string was null during setup.");

            Setup(_rabbitMQUri, prefetchSize, prefetchCount);
        }

        /// <inheritdoc/>
        protected void Setup(string rabbitMQUri, int prefetchSize = 0, int prefetchCount = 0)
        {
            if (setup)
                throw new InvalidOperationException("RabbitMQInterface was already setup, use Reset() to reset the instance.");

            _rabbitMQUri = rabbitMQUri;

            if (string.IsNullOrEmpty(_rabbitMQUri))
                throw new InvalidOperationException("Provided connection uri was empty.");

            _rabbitMQUri = rabbitMQUri;
            _prefetchSize = prefetchSize;
            _prefetchCount = prefetchCount;

            CreateConnection();

            CreateChannel();

            CreateConsumer();

            setup = true;
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
        private void CreateConnection(int retry = 0)
        {
            try
            {
                ConnectionFactory factory = new ConnectionFactory();
                factory.AutomaticRecoveryEnabled = true;
                factory.TopologyRecoveryEnabled = true;

                factory.Uri = new Uri(_rabbitMQUri);
                _connection = factory.CreateConnection();
            }
            catch (Exception)
            {
                if (RETRY_TIMES <= retry)
                {
                    throw;
                }

                Thread.Sleep(5000 * retry);
                CreateConnection(retry + 1);
            }
        }

        /// <summary>
        /// Create model.
        /// </summary>
        private void CreateChannel()
        {
            if (_connection == null)
                throw new InvalidOperationException("RabbitMQ connection object was null while creating a channel.");

            if (!_connection.IsOpen)
                throw new InvalidOperationException("RabbitMQ connection was closed while creating a channel.");

            try
            {
                _channel = _connection.CreateModel();
                _channel.BasicQos(prefetchSize: (uint)_prefetchSize, prefetchCount: (ushort)_prefetchCount, global: false);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Create the consumer.
        /// </summary>
        /// <returns></returns>
        private void CreateConsumer()
        {
            if (_channel == null)
                throw new InvalidOperationException("RabbitMQ channel object was null while creating a consumer.");

            if (!_channel.IsOpen)
                throw new InvalidOperationException("RabbitMQ channel was closed while creating a channel.");

            try
            {
                _basicConsumer = new EventingBasicConsumer(_channel);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}