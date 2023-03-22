using RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPC.Services
{
    internal sealed class MiddlewareProvider : ICloneable
    {
        //public delegate Task MiddlewareDelegate(RabbitMQRPCMessage message, Func<Task> next);

        private Queue<Func<RabbitMQRPCMessage, Func<Task>, Task>> _middleWares { get; set; } = new Queue<Func<RabbitMQRPCMessage, Func<Task>, Task>>();

        internal MiddlewareProvider Use(Func<RabbitMQRPCMessage, Func<Task>, Task> step)
        {
            _middleWares.Enqueue(step);
            return this;
        }

        internal async Task Run(RabbitMQRPCMessage context)
        {
            if (_middleWares.Any())
            {
                await RunInternal(context);
            }
        }

        private async Task RunInternal(RabbitMQRPCMessage context)
        {
            if (_middleWares.Count <= 0)
                return;

            await _middleWares.Dequeue()(context, () => RunInternal(context));      
        }

        public object Clone()
        {
            var provider = new MiddlewareProvider()
            {
                _middleWares = new Queue<Func<RabbitMQRPCMessage, Func<Task>, Task>>(_middleWares.ToList())
            };
            return provider;
        }
    }
}
