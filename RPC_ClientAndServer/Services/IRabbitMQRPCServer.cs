using System.Reflection;

namespace RPC.Services
{
    public interface IRabbitMQRPCServer
    {
        bool Setup();
        bool Setup(string rabbitMQUri);
        //void Subscribe(MethodInfo method, string? name = null, object? instance = null);
        void Subscribe(Delegate del, string name);
    }
}