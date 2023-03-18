using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RPC.Services;

namespace RPC.Extensions
{
    public static class Extensions
    {
        /// <summary>
        /// Add and interface to CoreMap/Flightmap with all the dependent services.
        /// The ICoreMapInterface service is the entry point to CoreMap/Flightmap.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>IHostBuilder builder</returns>
        public static IHostBuilder AddRabbitMQRPC(this IHostBuilder builder, string rabbitMQUri = "")
        {
            if (string.IsNullOrEmpty(rabbitMQUri))
            {
                return builder.ConfigureServices((c, s) =>
                {
                    s.AddSingleton<IRabbitMQRPCClient, RabbitMQRPCClient>();
                    s.AddSingleton<IRabbitMQRPCServer, RabbitMQRPCServer>();
                });
            }

            return builder.ConfigureServices((c, s) =>
            {
                s.AddSingleton<IRabbitMQRPCClient>(x =>
                {
                    var client = new RabbitMQRPCClient();
                    client.SetRabbitMQURI(rabbitMQUri);
                    return client;
                });

                s.AddSingleton<IRabbitMQRPCServer>(x =>
                {
                    var server = new RabbitMQRPCServer();
                    server.SetRabbitMQURI(rabbitMQUri);
                    return server;
                });
            });
        }
    }
}
