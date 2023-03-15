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

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);

//Register services
hostBuilder.AddRabbitMQRPC("amqp://guest:guest@localhost:5672");
IHost host = hostBuilder.Build();

host.StartAsync();

//Get service
using var scope = host.Services.CreateScope();
var server = scope.ServiceProvider.GetService<IRabbitMQRPCServer>();

if (server == null)
    return;

//Setup server instance
server?.Setup();

//Subscribe a delegate.
server.Subscribe((int x) => { return x + 1; }, "RPC_101");
```
Note that any parameters and returns must be serializable by Newtonsoft.Json.

### Client in console application.
```
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPC.Extensions;
using RPC.Services;

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);

//Register services
hostBuilder.AddRabbitMQRPC("amqp://guest:guest@localhost:5672");
IHost host = hostBuilder.Build();

host.StartAsync();

//Get service
using var scope = host.Services.CreateScope();
var client = scope.ServiceProvider.GetService<IRabbitMQRPCClient>();

if (client == null)
    return;

//Setup client instance
client?.Setup();

//Call a remote method
int result = client.Call<int>("RPC_101", 1);
```
