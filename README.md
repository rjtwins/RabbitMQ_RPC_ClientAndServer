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

### Client example in a console application.
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

### Middleware
Both client and server support middleware wrapping around their operations.
Client:
```
//Wraps around a call.
client.CallerUse(async (message, next) =>
{
    Debug.WriteLine("before caller");
    await next();
    Debug.WriteLine("after caller");
});

//Wraps around receiving a reply.
client.ReplyReceiverUse(async (message, next) =>
{
    Debug.WriteLine("before receiver");
    await next();
    Debug.WriteLine("after receiver");
});
```
Server:
```
//Wraps around receiving and processing a message.
server.ReceiverUse(async (context, next) =>
{
    Debug.WriteLine("before receive");
    await next();
    Debug.WriteLine("after receive");
});

//Wraps around responding after processing a message.
server.ResponderUse(async (context, next) =>
{
    Debug.WriteLine("before respond");
    await next();
    Debug.WriteLine("after respond");
});
```
