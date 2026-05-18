# What is an Actor?

An **actor** in Trupe is a self-contained unit of computation that processes messages one at a time. It is the fundamental building block of any Trupe application.

## Key Characteristics

- **Isolated State** — Each actor maintains its own private state. No other actor or code can directly read or modify it.
- **Sequential Message Processing** — Messages arrive in the actor's mailbox and are processed in order, one at a time. There is no need for locks or synchronization within an actor.
- **Asynchronous Communication** — Actors communicate by sending messages to each other through `IActorReference`, never by calling methods directly.
- **Lifecycle Hooks** — Actors have well-defined lifecycle events: initialization, restart, and cleanup.

## The `IActor` Interface

At its core, every actor implements the `IActor` interface:

```csharp
public interface IActor
{
    IActorContext Context { get; set; }
    ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default);
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default);
    ValueTask AfterRestartAsync(CancellationToken cancellationToken = default);
}
```

| Member | Description |
|--------|-------------|
| `Context` | Provides access to the actor's reference (`Self`) and the ability to set a response. |
| `HandleAsync` | Called for each incoming message. |
| `InitializeAsync` | Called once when the actor starts. |
| `BeforeRestartAsync` | Called before the actor is restarted (used for cleanup). |
| `AfterRestartAsync` | Called after the actor has been restarted. |

## The `Actor` Base Class

In practice, you extend the `Actor` base class instead of implementing `IActor` directly. It provides sensible defaults and makes it easy to get started:

```csharp
public abstract class Actor : IActor
{
    public IActorContext Context { get; set; }

    public virtual ValueTask HandleAsync(object? message, CancellationToken cancellationToken = default);
    public virtual ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    public virtual ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default);
    public virtual ValueTask AfterRestartAsync(CancellationToken cancellationToken = default);
}
```

The default `HandleAsync` implementation throws an `UnhandleMessageException` if no typed handler is found. You typically don't override it directly — instead, you implement `IHandleActorMessage<T>`.

## Typed Message Handling

The preferred way to handle messages is by implementing `IHandleActorMessage<T>` for each message type your actor supports:

```csharp
public interface IHandleActorMessage<TMessage>
{
    ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}
```

This provides:

- **Type safety** — the compiler ensures you handle the correct type.
- **Separation** — each message type gets its own handler method.
- **Multiple handlers** — an actor can implement as many `IHandleActorMessage<T>` interfaces as it needs.

## Actor Context

Every actor has access to a `Context` object of type `IActorContext`:

```csharp
public interface IActorContext : IAsyncDisposable
{
    IActorReference Self { get; }
    object? Response { get; set; }
    IServiceProvider ServiceProvider { get; }
}
```

| Property | Description |
|----------|-------------|
| `Self` | The actor's own `IActorReference`. Useful for passing your reference to other actors. |
| `Response` | Set this property to provide a return value when responding to an `Ask` request. |
| `ServiceProvider` | A scoped `IServiceProvider` for the current message. Use it to resolve scoped services within a message handler. |

A new `IActorContext` (and its associated DI scope) is created for **each message** processed by the actor. This means scoped services resolved through `Context.ServiceProvider` are isolated per message and automatically disposed after processing.

## Communicating with Actors

You never call methods on an actor directly. Instead, you interact through `IActorReference`:

### Fire-and-Forget (Tell)

Send a message without waiting for a response:

```csharp
actorRef.Tell(new Greet("World"));

// Or the async version
await actorRef.TellAsync(new Greet("World"));
```

### Request-Response (Ask)

Send a message and wait for a response:

```csharp
var result = await actorRef.AskAsync<int>(new Add(2, 3));
Console.WriteLine(result); // 5
```

The actor sets the response through `Context.Response`:

```csharp
public ValueTask HandleAsync(Add message, CancellationToken cancellationToken = default)
{
    Context.Response = message.A + message.B;
    return ValueTask.CompletedTask;
}
```

### Message Metadata

All `Tell` and `Ask` methods accept an optional `Dictionary<string, object>` metadata parameter. Metadata lets you attach contextual information (such as correlation IDs or tracing headers) to messages without changing your message types:

```csharp
// Fire-and-forget with metadata
actorRef.Tell(new Greet("World"), new Dictionary<string, object>
{
    ["correlationId"] = Guid.NewGuid().ToString()
});

// Request-response with metadata
var result = await actorRef.AskAsync<int>(new Add(2, 3), new Dictionary<string, object>
{
    ["source"] = "api"
});
```

### Termination Events

You can subscribe to an actor's termination event:

```csharp
actorRef.OnTerminate += (sender, args) =>
{
    Console.WriteLine($"Actor terminated: {args.Reason}");
};
```

## Next Steps

- [Creating an Actor](creating-an-actor.md) — step-by-step guide to building your first actor.
- [Actor Lifecycle](actor-lifecycle.md) — deep dive into initialization, restart, and cleanup hooks.
- [Mailboxes](mailboxes.md) — understanding how messages are queued and delivered.
