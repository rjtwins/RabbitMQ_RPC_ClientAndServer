using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; // Requires NuGet package
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

List<long> times = new List<long>();

Parallel.For(0, 500, i =>
{
    var task = client.CallAsync<int>("RPC_7", i);
    task.Wait();
    var result = task.Result;
    Console.WriteLine(result);

});

Console.WriteLine("Press any key to exit");
Console.ReadKey();