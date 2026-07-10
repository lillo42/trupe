# Pipelines and Middleware

Trupe uses a **pipeline architecture** for both sending and receiving messages. Middlewares are functions that sit in the pipeline and can inspect, modify, or short-circuit message processing.

## Pipeline Overview

Every message in Trupe flows through two pipelines:

```
                    SEND PIPELINE                           RECEIVE PIPELINE
               (ActorReference side)                      (ActorProcess side)

Tell/Ask ──► [Middleware 1] ──► [Middleware 2] ──► Mailbox ──► [Middleware 1] ──► [Middleware 2] ──► Actor.HandleAsync
```

- **Send Pipeline** — executes when a message is sent to an actor via `Tell` or `Ask`. The built-in `ActorProcessDispatcherMiddleware` delivers the message to the actor's mailbox.
- **Receive Pipeline** — executes when an actor processes a message from its mailbox. The built-in `ActorMessageDispatcherMiddleware` dispatches the message to the typed handler.

## Built-in Middlewares

| Middleware | Pipeline | Order | Description |
|-----------|----------|-------|-------------|
| `AskMiddleware` | Receive | `int.MinValue` | Handles request-response (`Ask`) pattern by managing response completion. |
| `ActorMessageDispatcherMiddleware` | Receive | `int.MaxValue` | Dispatches messages to typed `IHandleActorMessage<T>` handlers or the untyped `HandleAsync(object?)` fallback. |
| `ActorProcessDispatcherMiddleware` | Send | `int.MaxValue` | Delivers the message to the actor process mailbox. |

## Creating a Middleware

Middlewares implement either `ISendMiddleware` or `IReceiveMiddleware`:

```csharp
public interface ISendMiddleware : IMiddleware
{
    ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next);
}

public interface IReceiveMiddleware : IMiddleware
{
    ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next);
}
```

### Example: Logging Middleware

```csharp
public class LoggingMiddleware : ISendMiddleware, IReceiveMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
    {
        _logger.LogInformation("Sending {MessageType} to {ActorType}",
            context.Message.Payload.GetType().Name,
            context.ActorType.Name);

        await next(context);
    }

    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        _logger.LogInformation("Actor {ActorType} receiving {MessageType}",
            context.Actor.GetType().Name,
            context.Message.Payload.GetType().Name);

        await next(context);
    }
}
```

### Example: Metrics Middleware

```csharp
public class MetricsMiddleware : IReceiveMiddleware
{
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            // Record processing duration
        }
    }
}
```

## Registering Middlewares

### Global Middleware

A global middleware applies to **all actors** in the system. Register it with `Use<TMiddleware>()`:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<GreeterActor>();

    // Global middleware — applies to all actors
    config.Use<LoggingMiddleware>();

    // With explicit order (lower runs first)
    config.Use<MetricsMiddleware>(order: 1);

    // With metadata
    config.Use<AuditMiddleware>(order: 2, metadata: new AuditConfig { Level = "full" });
});
```

### Per-Actor Middleware

A per-actor middleware applies only to a specific actor type. Configure it via the `configure` parameter on `AddActor`:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<OrderActor>(actor =>
    {
        actor.Use<ValidationMiddleware>(order: 0);
        actor.Use<AuditMiddleware>(order: 1, metadata: new AuditConfig { Level = "strict" });
    });

    // GreeterActor has no extra middlewares
    config.AddActor<GreeterActor>();
});
```

### Per-Message Middleware

You can target a middleware to a specific message type for a specific actor:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<OrderActor>(actor =>
    {
        // Only runs for PlaceOrder messages
        actor.UseForMessage<ValidationMiddleware, PlaceOrder>(order: 0);
    });
});
```

### Attribute-Based Middleware

You can also declare middlewares directly on handler methods using `[Middleware]` attributes:

```csharp
[Middleware(typeof(CachingMiddleware), Order = -1, Scope = MiddlewareScope.Receive)]
public ValueTask HandleAsync(GetProduct message, CancellationToken cancellationToken = default)
{
    // ...
}
```

If `Scope` is omitted, it defaults based on the middleware type's implemented interfaces (`ISendMiddleware` and/or `IReceiveMiddleware`).

## Middleware Execution Order

Middlewares execute in ascending `Order` value:

1. Built-in `AskMiddleware` (order: `int.MinValue`) — always first in receive pipeline
2. Your middlewares (ordered by `Order` value)
3. Built-in `ActorMessageDispatcherMiddleware` / `ActorProcessDispatcherMiddleware` (order: `int.MaxValue`) — always last

Within the same order, middlewares are invoked in registration order.

## Pipeline Context

### `ISendPipelineContext`

Available in send middlewares:

| Property | Description |
|----------|-------------|
| `Message` | The message being sent. |
| `ActorType` | The type of the target actor. |
| `Target` | The `IActorReference` of the target actor. |
| `Items` | A mutable dictionary for data shared between middlewares in the same pipeline execution. |
| `Metadata` | Pipeline metadata collection (from middleware configs). |
| `ServiceProvider` | Scoped service provider for this pipeline execution. |
| `CancellationToken` | Cancellation token. |

### `IReceivePipelineContext`

Available in receive middlewares:

| Property | Description |
|----------|-------------|
| `Message` | The message being processed. |
| `Actor` | The actor instance processing the message. |
| `ActorContext` | The actor's context (includes `Self`, `Response`, `ServiceProvider`). |
| `Items` | A mutable dictionary for data shared between middlewares in the same pipeline execution. |
| `Metadata` | Pipeline metadata collection. |
| `ServiceProvider` | Scoped service provider for this pipeline execution. |
| `CancellationToken` | Cancellation token. |

## Pipeline Metadata

Middlewares can attach metadata during registration that is accessible at runtime:

```csharp
// Registration
config.Use<CachingMiddleware>(metadata: new CacheOptions { Duration = TimeSpan.FromMinutes(5) });

// Inside middleware
public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
{
    var options = context.Metadata.GetMetadata<CacheOptions>();
    if (options != null)
    {
        // Use cache options...
    }
    await next(context);
}
```

## Registering Middleware in DI

If your middleware has dependencies, register it in DI using `AddMiddleware`:

```csharp
services.AddTrupe(config =>
{
    // Register with DI (resolved via constructor injection)
    config.AddMiddleware<LoggingMiddleware>();

    // Register with factory
    config.AddMiddleware<MetricsMiddleware>(
        sp => new MetricsMiddleware(sp.GetRequiredService<IMeterFactory>()),
        lifetime: ServiceLifetime.Singleton
    );

    // Then add to pipeline
    config.Use<LoggingMiddleware>();
    config.Use<MetricsMiddleware>(order: 1);
});
```

By default, `Use<TMiddleware>()` automatically registers the middleware as transient if not already registered.

## Next Steps

- [Dependency Injection](dependency-injection.md) — full DI integration guide.
- [AOT Compatibility](aot-compatibility.md) — AOT considerations for middleware and pipelines.
- [Getting Started](getting-started.md) — build a complete application.
