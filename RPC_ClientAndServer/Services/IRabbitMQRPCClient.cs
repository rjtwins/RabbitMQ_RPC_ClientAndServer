namespace RPC.Services
{
    public interface IRabbitMQRPCClient
    {
        void Call(params object[] arguments);
        void Call(string alias, params object[] arguments);
        T Call<T>(params object[] arguments);
        T Call<T>(string alias, params object[] arguments);
        Task<T> CallAsync<T>(params object[] arguments);
        Task<T> CallAsync<T>(string alias, params object[] arguments);
        bool Setup(string rabbitMQUri);
        bool Setup();
    }
}