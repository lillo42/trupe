# Trupe

A lightweight, high-performance .NET implementation of the Actor Model for learning and practical use.

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![AOT Compatible](https://img.shields.io/badge/AOT-Compatible-green.svg)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

## Overview

Trupe is an actor model framework for .NET that provides a simple yet powerful way to build concurrent and distributed applications. Actors are isolated units of computation that communicate exclusively through asynchronous message passing, eliminating shared state and the complexity of traditional multi-threaded programming.

### Key Features

- **Simple API** - Easy-to-use abstractions for creating actors and handling messages
- **Type-Safe Message Handling** - Strongly-typed message handlers with compile-time checking
- **Supervision Strategies** - Built-in support for One-for-One and All-for-One supervision
- **AOT Compatible** - Full support for Native Ahead-of-Time compilation
- **High Performance** - Channel-based mailboxes for efficient message delivery
- **Lifecycle Management** - Initialize, restart, and cleanup hooks for actors

## Installation

```bash
dotnet add package Trupe
```

For hosting integration (automatic start/stop with the application host):

```bash
dotnet add package Trupe.Extensions.Hosting
```

## Quick Start

### Creating an Actor

```csharp
using Trupe;

// Define a message
public record Greet(string Name);

// Create an actor with type-safe message handling
public class GreeterActor : Actor, IHandleActorMessage<Greet>
{
    public ValueTask HandleAsync(Greet message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }
}
```

### Request-Response Pattern (Ask)

```csharp
public record Add(int A, int B);

public class CalculatorActor : Actor, IHandleActorMessage<Add>
{
    public ValueTask HandleAsync(Add message, CancellationToken cancellationToken = default)
    {
        var result = message.A + message.B;
        Context.Response = result;  // Set the response
        return ValueTask.CompletedTask;
    }
}

// Usage
var result = await actorRef.AskAsync<Add, int>(new Add(2, 3));
Console.WriteLine(result);  // Output: 5
```

### Fire-and-Forget Pattern (Tell)

```csharp
// Send a message without waiting for a response
actorRef.Tell(new Greet("World"));

// Or async version
await actorRef.TellAsync(new Greet("World"));
```

## Supervision

Supervisors manage child actors and handle failures with configurable strategies:

```csharp
public class MySupervisor : Supervisor
{
    public MySupervisor(IActorFactory actorFactory, ILogger<MySupervisor> logger) 
        : base(actorFactory, logger) { }

    // Supervision configuration
    protected override Strategy Strategy => Strategy.OneForOne;
    protected override int MaxRestarts => 3;
    protected override TimeSpan RestartWindow => TimeSpan.FromSeconds(5);

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        // Add child actors during initialization
        AddChild<WorkerActor>();
        AddChild<AnotherWorkerActor>();
        
        return ValueTask.CompletedTask;
    }
}
```

### Supervision Strategies

| Strategy | Description |
|----------|-------------|
| `OneForOne` | Only the failed actor is affected by the supervision action |
| `AllForOne` | All sibling actors are affected when one fails |

### Failure Actions

| Action | Description |
|--------|-------------|
| `Restart` | Restart the failed actor with fresh state |
| `Stop` | Stop the actor permanently |
| `Escalate` | Escalate the failure to the parent supervisor |
| `Resume` | Resume the actor without restarting |

## Actor Lifecycle

Actors have several lifecycle hooks you can override:

```csharp
public class MyActor : Actor
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Called once when the actor starts
        return ValueTask.CompletedTask;
    }

    public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
    {
        // Called before the actor is restarted (cleanup)
        return ValueTask.CompletedTask;
    }

    public override ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
    {
        // Called after the actor has been restarted
        return ValueTask.CompletedTask;
    }
}
```

## Mailboxes

Trupe uses channel-based mailboxes for message delivery:

```csharp
// Unbounded mailbox (default)
var mailbox = new ChannelMailbox();

// Bounded mailbox with capacity limit
var boundedMailbox = new ChannelMailbox(maxSize: 100);

// Bounded mailbox with custom full behavior
var mailbox = new ChannelMailbox(
    maxSize: 100, 
    fullMode: BoundedChannelFullMode.DropOldest
);
```

## AOT Compatibility

Trupe is fully compatible with .NET Native AOT compilation. When running in AOT mode, the framework automatically falls back to untyped message handling:

```csharp
public class AotCompatibleActor : Actor
{
    public override ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default)
    {
        return message switch
        {
            Greet greet => HandleGreet(greet),
            Add add => HandleAdd(add),
            _ => base.HandleAsync(message, cancellationToken)
        };
    }

    private ValueTask HandleGreet(Greet message) { /* ... */ }
    private ValueTask HandleAdd(Add message) { /* ... */ }
}
```

## Dependency Injection

Register the actor system using the `AddTrupe` extension method:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddSupervisor<MySupervisor>();
    config.ConfigureRootSupervisor(options => { /* ... */ });
});
```

### Hosting Integration

Use `Trupe.Extensions.Hosting` to automatically start and stop the actor system with the application host:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();
    config.AddHostedService();
});
```

### Actor Registry

The `IActorRegister` provides a thread-safe registry for looking up actors by identifier:

```csharp
// Register an actor reference
actorRegister.Register("my-actor", actorRef);

// Look up an actor
var actor = actorRegister.Get("my-actor");

// Safe lookup
if (actorRegister.TryGet("my-actor", out var actorRef))
{
    actorRef.Tell(new Greet("World"));
}
```

## Packages

| Package | Description |
|---------|-------------|
| `Trupe.Abstractions` | Core interfaces and abstractions (IActor, ISupervisor, IActorReference, etc.) |
| `Trupe` | Actor model implementation with supervision, mailboxes, and DI integration |
| `Trupe.Extensions.Hosting` | IHostedService integration for managing actor system lifecycle |

## Requirements

- .NET 8.0, 9.0, or 10.0

## License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! This project was created for learning purposes, and we encourage experimentation and improvements.
