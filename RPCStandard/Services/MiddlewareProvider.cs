using RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPC.Services
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    internal class MiddlewareProvider<T1, T2> : ICloneable
    {
        protected Queue<Func<T1, Func<Task<T2>>, Task<T2>>> _middleWares { get; set; } = new Queue<Func<T1, Func<Task<T2>>, Task<T2>>>();
        internal Func<T1, Func<Task<T2>>, Task<T2>> Final { get; set; }

        internal MiddlewareProvider<T1, T2> Use(Func<T1, Func<Task<T2>>, Task<T2>> step)
        {
            _middleWares.Enqueue(step);
            return this;
        }

        internal async Task<T2> Run(T1 context)
        {
            if (_middleWares.Any())
            {
                return await RunInternal(context);
            }

            return await Final(context, null);
        }

        private async Task<T2> RunInternal(T1 context)
        {
            if (_middleWares.Count <= 0)
                return await Final(context, null);

            return await _middleWares.Dequeue()(context, () => RunInternal(context));
        }

        public object Clone()
        {
            var provider = new MiddlewareProvider<T1, T2>()
            {
                _middleWares = new Queue<Func<T1, Func<Task<T2>>, Task<T2>>>(_middleWares.ToList()),
                Final = Final
            };
            return provider;
        }
    }
}
