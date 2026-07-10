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
    public AppSupervisor(ILogger<AppSupervisor> logger)
        : base(logger) { }

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

The easiest way to send messages to child actors is to capture the `IActorReference` returned by `AddChild` in your supervisor:

```csharp
public class AppSupervisor : Supervisor
{
    private IActorReference _greeterRef = null!;
    private IActorReference _calculatorRef = null!;

    public AppSupervisor(ILogger<AppSupervisor> logger)
        : base(logger) { }

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        _greeterRef = AddChild<GreeterActor>();
        _calculatorRef = AddChild<CalculatorActor>();
        return ValueTask.CompletedTask;
    }

    public void Greet(string name) => _greeterRef.Tell(new Greet(name));

    public async Task<int> AddAsync(int a, int b)
        => await _calculatorRef.AskAsync<int>(new Add(a, b));
}
```

Alternatively, you can resolve an actor by its URI through `IActorProcessRegistry`:

```csharp
var registry = serviceProvider.GetRequiredService<IActorProcessRegistry>();
var greeterRef = new ActorReference("greeter", registry);

greeterRef.Tell(new Greet("World"));
```

> **Note:** `IActorProcessRegistry` maps actor references to their running processes. `ActorReference` uses the registry to look up the actual runtime reference by name or URI. If the actor is not found, operations on the returned reference will behave as dead letters.

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
    public AppSupervisor(ILogger<AppSupervisor> logger)
        : base(logger) { }

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
