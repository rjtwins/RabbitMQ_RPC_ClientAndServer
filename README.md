# RabbitMQ_RPC_ClientAndServer
RPC client and server for RabbitMQ

## Introduction

## Setup
Option 1 - Add to the ServiceCollection via IHostBuilder extension method in RPC.Extensions.
    Optionally provide a rabbitMQ connection uri.

Option 2 - Instantiate a new instance of RabbitMQRPCClient and or RabbitMQRPCServer.

When an instance of either has been instantiated call .Setup() or .Setup(rabbitMQUri) depending on if a connection uri was provided during registering of the service.

## Usage
### Server example in a console application.
```
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
var server = scope.ServiceProvider.GetService<IRabbitMQRPCServer>();

server?.Setup();

if (server == null)
    return;

server.Subscribe((int x) => 
{ 
    return x + 1;
}, "RPC_101", true);

Console.WriteLine("Press any key to exit");
Console.ReadKey();
```
Note that any parameters and returns must be serializable by Newtonsoft.Json.

### Client example in a console application.
```
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

var result = client.Call<int>("RPC_101", 1);
Console.WriteLine(result);

Console.WriteLine("Press any key to exit");
Console.ReadKey();
```

### Middleware
Both client and server support middleware.
Client:
```
//Wraps around a call.
client.CallerUse(async (message, next) =>
{
    Debug.WriteLine("before caller");
    var result = await next();
    Debug.WriteLine("after caller");

    return result;
});
```
Server:
```
//Wraps around receiving and processing a message.
server.ReceiverUse(async (context, next) =>
{
    Debug.WriteLine("before receive");
    var result = await next();
    Debug.WriteLine("after receive");
    return result;
});
```
