using System;
using System.Reflection;

namespace RPC.Services
{
    public interface IRabbitMQRPCServer
    {
        /// <summary>
        /// Setup server with predifined rabbitMQ uri.
        /// Optional argument RPC Queue expiration time.
        /// Where the expiration time is the time sinse last interaction with queue.
        /// Default expiration time is 36 hours.
        /// </summary>
        /// <param name="QueuExpireTime"></param>
        /// <returns></returns>
        void Setup(int? QueuExpireTime = null);

        /// <summary>
        /// Setup server with given rabbitMQ uri.
        /// Optional argument RPC Queue expiration time.
        /// Where the expiration time is the time sinse last interaction with queue.
        /// Default expiration time is 36 hours.
        /// </summary>
        /// <param name="rabbitMQUri"></param>
        /// <param name="QueuExpireTime"></param>
        /// <returns></returns>
        void Setup(string rabbitMQUri, int? QueuExpireTime = null);

        /// <summary>
        /// Subscribe a Delegate to be called via RPC with given alias.
        /// Any parameters and return types for delegate must be serializable via Newtonsoft.Json.
        /// </summary>
        /// <remarks>
        /// <para>
        /// async indicates if server should handle delegate call async.
        /// </para>
        /// </remarks>
        /// <param name="del"></param>
        /// <param name="name"></param>
        /// <param name="async"></param>
        void Subscribe(Delegate del, string name, bool async = false);
    }
}