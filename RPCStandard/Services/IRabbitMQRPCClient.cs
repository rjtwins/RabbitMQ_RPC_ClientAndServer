using System.Threading.Tasks;

namespace RPC.Services
{
    public interface IRabbitMQRPCClient
    {
        /// <summary>
        /// Setup client with predefined RabbitMQUri.
        /// </summary>
        /// <returns></returns>
        void Setup();

        /// <summary>
        /// Setup client.
        /// </summary>
        /// <param name="rabbitMQUri"></param>
        /// <returns></returns>
        void Setup(string rabbitMQUri);

        /// <summary>
        /// RPC function where the alias of the remote function is the name of the calling function.
        /// </summary>
        /// <param name="arguments"></param>
        void Call(params object[] arguments);

        /// <summary>
        /// RPC function with alias.
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="arguments"></param>
        void Call(string alias, params object[] arguments);

        /// <summary>
        /// RPC function where the alias of the remote function is the name of the calling function.
        /// With return type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arguments"></param>
        /// <returns></returns>
        T Call<T>(params object[] arguments);

        /// <summary>
        /// RPC function with alias.
        /// With return type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="alias"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        T Call<T>(string alias, params object[] arguments);

        /// <summary>
        /// Async RPC function where the alias of the remote function is the name of the calling function.
        /// With return type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arguments"></param>
        /// <returns></returns>
        Task<T> CallAsync<T>(params object[] arguments);

        /// <summary>
        /// Async RPC function with alias.
        /// With return type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="alias"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        Task<T> CallAsync<T>(string alias, params object[] arguments);
    }
}