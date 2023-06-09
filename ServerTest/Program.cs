﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPC.Extensions;
using RPC.Services;
using System.Diagnostics;

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);
hostBuilder.AddRabbitMQRPC("amqp://guest:guest@localhost:5672");
IHost host = hostBuilder.Build();

host.StartAsync();

using var scope = host.Services.CreateScope();
var server = scope.ServiceProvider.GetService<IRabbitMQRPCServer>();

server?.Setup();

if (server == null)
    return;

server.Subscribe((int x) => 
{ 
    return x + 1;
}, "RPC_101", true);

server.ReceiverUse(async (context, next) =>
{
    Debug.WriteLine("before receive");
    var result = await next();
    Debug.WriteLine("after receive");
    return result;
});

Console.WriteLine("Press any key to exit");
Console.ReadKey();