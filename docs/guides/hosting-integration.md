# Hosting Integration

The `Trupe.Extensions.Hosting` package integrates the actor system with the .NET Generic Host, automatically starting and stopping the actor system with your application.

## Installation

```bash
dotnet add package Trupe.Extensions.Hosting
```

## Setup

Register the hosted service during configuration:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddSupervisor<AppSupervisor>();
    config.AddHostedService(); // ← Adds the hosted service
});

var app = builder.Build();
app.Run();
```

Or register it separately:

```csharp
builder.Services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddSupervisor<AppSupervisor>();
});

// Add the hosted service separately
builder.Services.AddActorSystemHostedService();
```

## How It Works

The `ActorSystemHostedService` implements `IHostedService`:

```csharp
public class ActorSystemHostedService(ActorSystem system) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await system.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await system.StopAsync();
    }
}
```

| Event | Action |
|-------|--------|
| Application starts | `ActorSystem.StartAsync()` is called, which initializes the root supervisor and all child actors. |
| Application stops | `ActorSystem.StopAsync()` is called, which gracefully shuts down all actors in the hierarchy. |

## With ASP.NET Core

Trupe works seamlessly with ASP.NET Core applications. A common pattern is to expose actor references through a supervisor registered as a singleton, or to resolve them through `IActorProcessRegistry`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<OrderProcessorActor>();
    config.AddSupervisor<OrderSupervisor>();
    config.AddHostedService();
});

var app = builder.Build();

app.MapPost("/orders", async (PlaceOrder order, IActorProcessRegistry registry) =>
{
    var actorRef = registry.GetReference(new Uri("trupe://localhost/order-processor"));
    var result = await actorRef.AskAsync<OrderResult>(order);
    return result is not null ? Results.Ok(result) : Results.StatusCode(503);
});

app.Run();
```

## With Worker Services

Ideal for background processing applications:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<DataIngestionActor>();
    config.AddActor<DataProcessorActor>();
    config.AddSupervisor<DataPipelineSupervisor>();
    config.AddHostedService();
});

var app = builder.Build();
app.Run();
```

The actor system starts automatically when the worker service starts and stops gracefully when the service shuts down.

## Next Steps

- [Getting Started](getting-started.md) — build a complete application.
- [Dependency Injection](dependency-injection.md) — advanced DI configuration.
- [AOT Compatibility](aot-compatibility.md) — using Trupe with Native AOT.
