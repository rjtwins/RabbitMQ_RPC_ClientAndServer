using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPC.Extensions;
using RPC.Services;
using System.Diagnostics;

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);
hostBuilder.AddRabbitMQRPC("amqp://guest:guest@localhost:5672");
IHost host = hostBuilder.Build();

host.StartAsync();

using var scope = host.Services.CreateScope();
var client = scope.ServiceProvider.GetService<IRabbitMQRPCClient>();

client?.Setup();
if (client == null)
    return;

client.CallerUse(async (message, next) =>
{
    Debug.WriteLine("before caller");
    var result = await next();
    Debug.WriteLine("after caller");

    return result;
});


var result = client.Call<int>("RPC_101", 1);
Console.WriteLine(result);

Console.WriteLine("Press any key to exit");
Console.ReadKey();