# Getting Started

This guide walks you through setting up a Trupe application from scratch.

## Installation

Add the Trupe package to your .NET project:

```bash
dotnet add package Trupe
```

For automatic start/stop with the .NET Generic Host:

```bash
dotnet add package Trupe.Extensions.Hosting
```

## Step 1: Define Your Messages

Messages are the data that actors exchange. Use C# records for immutability:

```csharp
public record Greet(string Name);
public record Add(int A, int B);
```

## Step 2: Create an Actor

Create an actor by extending `Actor` and implementing `IHandleActorMessage<T>` for each message type:

```csharp
using Trupe;

public class GreeterActor : Actor, IHandleActorMessage<Greet>
{
    public ValueTask HandleAsync(Greet message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }
}

public class CalculatorActor : Actor, IHandleActorMessage<Add>
{
    public ValueTask HandleAsync(Add message, CancellationToken cancellationToken = default)
    {
        Context.Response = message.A + message.B;
        return ValueTask.CompletedTask;
    }
}
```

## Step 3: Create a Supervisor

Supervisors manage child actors and handle their failures:

```csharp
public class AppSupervisor : Supervisor
{
    public AppSupervisor(IActorFactory actorFactory, ILogger<AppSupervisor> logger)
        : base(actorFactory, logger) { }

    protected override Strategy Strategy => Strategy.OneForOne;
    protected override int MaxRestarts => 3;
    protected override TimeSpan RestartWindow => TimeSpan.FromSeconds(5);

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<GreeterActor>();
        AddChild<CalculatorActor>();
        return ValueTask.CompletedTask;
    }
}
```

## Step 4: Configure the Actor System

Register the actor system with dependency injection:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddActor<CalculatorActor>();
    config.AddSupervisor<AppSupervisor>();
    config.AddHostedService(); // Auto start/stop
});

var app = builder.Build();
app.Run();
```

## Step 5: Send Messages

Use the `IActorRegister` to look up actors and send messages:

```csharp
// Look up an actor
var register = serviceProvider.GetRequiredService<IActorRegister>();

if (register.TryGet("greeter", out var greeterRef))
{
    // Fire-and-forget
    greeterRef.Tell(new Greet("World"));
}

if (register.TryGet("calculator", out var calcRef))
{
    // Request-response
    var result = await calcRef.AskAsync<int>(new Add(2, 3));
    Console.WriteLine($"2 + 3 = {result}");
}
```

## Complete Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trupe;

// Messages
public record Greet(string Name);
public record Add(int A, int B);

// Actors
public class GreeterActor : Actor, IHandleActorMessage<Greet>
{
    public ValueTask HandleAsync(Greet message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }
}

public class CalculatorActor : Actor, IHandleActorMessage<Add>
{
    public ValueTask HandleAsync(Add message, CancellationToken cancellationToken = default)
    {
        Context.Response = message.A + message.B;
        return ValueTask.CompletedTask;
    }
}

// Supervisor
public class AppSupervisor : Supervisor
{
    public AppSupervisor(IActorFactory actorFactory, ILogger<AppSupervisor> logger)
        : base(actorFactory, logger) { }

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<GreeterActor>();
        AddChild<CalculatorActor>();
        return ValueTask.CompletedTask;
    }
}

// Program
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddActor<CalculatorActor>();
    config.AddSupervisor<AppSupervisor>();
    config.AddHostedService();
});

var app = builder.Build();
app.Run();
```

## Next Steps

- [Dependency Injection](dependency-injection.md) — advanced DI configuration.
- [Hosting Integration](hosting-integration.md) — managing the actor system lifecycle.
- [What is an Actor?](../actors/what-is-an-actor.md) — deep dive into actors.
- [Supervisors](../supervisors/what-is-a-supervisor.md) — learn about fault tolerance.
