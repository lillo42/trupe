# AOT Compatibility

Trupe is fully compatible with .NET Native Ahead-of-Time (AOT) compilation. This page explains how typed message dispatch works and what to do when running in AOT mode.

## How Typed Dispatch Works

In JIT mode, Trupe uses reflection to discover `IHandleActorMessage<T>` implementations and dispatch messages to the correct handler. This provides a clean, strongly-typed API.

In AOT mode, reflection-based dispatch may not be available because the compiler trims unused code paths. Trupe detects this and falls back to untyped message handling.

## AOT-Compatible Actors

When targeting AOT, override `HandleAsync(object?)` and use pattern matching to dispatch messages:

```csharp
public class AotCompatibleActor : Actor
{
    public override ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default)
    {
        return message switch
        {
            Greet greet => HandleGreet(greet, cancellationToken),
            Add add => HandleAdd(add, cancellationToken),
            _ => base.HandleAsync(message, cancellationToken)
        };
    }

    private ValueTask HandleGreet(Greet message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleAdd(Add message, CancellationToken cancellationToken)
    {
        Context.Response = message.A + message.B;
        return ValueTask.CompletedTask;
    }
}
```

## Best Practices

| Practice | Description |
|----------|-------------|
| Use pattern matching | Dispatch messages manually with `switch` expressions. |
| Call `base.HandleAsync` | For unhandled message types, let the base class throw `UnhandleMessageException`. |
| Keep messages simple | Use records or simple classes that are AOT-friendly. |
| Avoid reflection | Do not use reflection-based patterns in actor code. |

## Mixing JIT and AOT

You can write actors that work in both modes by implementing both `IHandleActorMessage<T>` and overriding `HandleAsync(object?)`. In JIT mode, the typed dispatch will be used. In AOT mode, the override will be used as a fallback.

```csharp
public class DualModeActor : Actor, IHandleActorMessage<Greet>
{
    // Used in JIT mode
    public ValueTask HandleAsync(Greet message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }

    // Fallback for AOT mode
    public override ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default)
    {
        return message switch
        {
            Greet greet => HandleAsync(greet, cancellationToken),
            _ => base.HandleAsync(message, cancellationToken)
        };
    }
}
```

## Next Steps

- [Getting Started](getting-started.md) — build a complete application.
- [Creating an Actor](../actors/creating-an-actor.md) — standard actor creation guide.
