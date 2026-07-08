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
    public Task StartAsync(CancellationToken cancellationToken)
    {
        system.Start();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await system.StopAsync();
    }
}
```

| Event | Action |
|-------|--------|
| Application starts | `ActorSystem.Start()` is called, which initializes the root supervisor and all child actors. |
| Application stops | `ActorSystem.StopAsync()` is called, which gracefully shuts down all actors in the hierarchy. |

## With ASP.NET Core

Trupe works seamlessly with ASP.NET Core applications:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<OrderProcessorActor>();
    config.AddSupervisor<OrderSupervisor>();
    config.AddHostedService();
});

var app = builder.Build();

app.MapPost("/orders", async (PlaceOrder order, IActorRegister register) =>
{
    if (register.TryGet("order-processor", out var actorRef))
    {
        var result = await actorRef.AskAsync<OrderResult>(order);
        return Results.Ok(result);
    }
    return Results.StatusCode(503);
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
