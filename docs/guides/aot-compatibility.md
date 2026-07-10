# AOT Compatibility

Trupe is fully compatible with .NET Native Ahead-of-Time (AOT) compilation. This page explains how typed message dispatch works, trimming annotations, and what to consider when running in AOT mode.

## How Typed Dispatch Works

In JIT mode, Trupe uses reflection to discover `IHandleActorMessage<T>` implementations and dispatch messages to the correct handler. This provides a clean, strongly-typed API.

In AOT mode, reflection-based dispatch may not be available because the compiler trims unused code paths. Trupe detects this at runtime via `RuntimeFeature.IsDynamicCodeSupported` and falls back to untyped message handling.

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

## Mixing JIT and AOT

You can write actors that work in both modes by implementing both `IHandleActorMessage<T>` and overriding `HandleAsync(object?)`. In JIT mode, the typed dispatch will be used. In AOT mode, the override will be used as a fallback.

```csharp
public class DualModeActor : Actor, IHandleActorMessage<Greet>
{
    // Used in JIT mode (typed dispatch via reflection)
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

## Trimming Annotations

Trupe uses `[DynamicallyAccessedMembers]` annotations throughout to inform the trimmer which type metadata must be preserved. When you register actors via `AddActor<TActor>()` or `AddSupervisor<TSupervisor>()`, the trimmer preserves the required members automatically.

### Key annotations in the framework

| Type/Member | Annotation | Purpose |
|-------------|-----------|---------|
| `ActorSystemConfigurator.AddActor<TActor>` | `PublicConstructors \| PublicMethods` on `TActor` | Preserves constructors for DI and methods for typed dispatch. |
| `ActorSystemConfigurator.AddActor(Type)` | `PublicConstructors \| PublicMethods` on `actorType` | Same as above for runtime type registration. |
| `ActorSystemConfigurator.AddSupervisor<TSupervisor>` | `PublicConstructors \| PublicMethods` on `TSupervisor` | Preserves supervisor constructors and methods. |
| `ActorSystemConfigurator.Use<TMiddleware>` | `PublicConstructors \| PublicMethods` on `TMiddleware` | Ensures middleware can be constructed and invoked. |
| `ActorConfigurator.UseForMessage` | `PublicConstructors \| PublicMethods` on middleware/actor types | Preserves per-actor and per-message middleware members. |
| `Supervisor.AddChild<TActor>` | `PublicConstructors \| PublicMethods` on `TActor` | Preserves members for child actors added at runtime. |
| `IChildSpecification.ActorType` | `PublicConstructors \| PublicMethods` | Preserves actor constructors and handler methods. |
| `ChildSpecification.ActorType` | `PublicConstructors \| PublicMethods` | Same as above for the concrete specification type. |
| Pipeline factory `Create` methods | `PublicMethods` on `actorType` | Enables middleware attribute discovery and handler resolution. |

### What this means for you

- **Use `AddActor<T>()` / `AddSupervisor<T>()`** — the generic type parameter carries trimming annotations, so the trimmer knows to preserve the required members.
- **Avoid passing actor types as untyped `Type` from dynamic sources** — if you resolve types dynamically (e.g., from configuration strings), the trimmer cannot statically know which members to preserve.
- **Custom middleware** — middleware types registered via `Use<TMiddleware>()` are annotated with `PublicConstructors`, so they work with trimming out of the box.

## Project Configuration

All Trupe source projects set `IsAotCompatible=true`:

```xml
<PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

This enables the IL trimming analyzers during build. If you see IL2067/IL2072/IL3050 warnings in your own code when calling Trupe APIs, it typically means you are passing a `Type` that lacks the required `[DynamicallyAccessedMembers]` annotation. Add the annotation to your parameter or use the generic overload instead.

## Best Practices

| Practice | Description |
|----------|-------------|
| Use generic registration APIs | `AddActor<T>()` carries trimming metadata; prefer over `AddActor(Type)`. |
| Use pattern matching in AOT | Override `HandleAsync(object?)` with a `switch` for direct dispatch. |
| Call `base.HandleAsync` | For unhandled message types, let the base class throw `UnhandleMessageException`. |
| Keep messages simple | Use records or simple classes that are AOT-friendly. |
| Avoid runtime reflection in actors | Do not use `MakeGenericType`/`MakeGenericMethod` in actor code targeting AOT. |
| Test with `PublishAot` | Validate your app with `dotnet publish -p:PublishAot=true` to catch trimming issues early. |

## Next Steps

- [Pipelines and Middleware](pipelines.md) — middleware system details.
- [Getting Started](getting-started.md) — build a complete application.
- [Creating an Actor](../actors/creating-an-actor.md) — standard actor creation guide.
