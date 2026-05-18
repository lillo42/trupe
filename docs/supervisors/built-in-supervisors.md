# Built-in Supervisors

Trupe provides several built-in supervisor implementations for common patterns. You can use them directly or extend them for custom behavior.

## RootSupervisor

The **RootSupervisor** is the top-level supervisor that sits at the root of the actor hierarchy. It is created automatically when you configure the actor system.

### Behavior

- Always restarts failed children (`FailureAction.Restart`).
- Initializes its children from `RootSupervisorOptions`.
- There is exactly one root supervisor per actor system.

### Configuration

```csharp
services.AddTrupe(config =>
{
    config.AddActor<WorkerActor>();
    config.AddSupervisor<MySupervisor>();

    config.ConfigureRootSupervisor(options =>
    {
        // Configure root supervisor options
    });
});
```

### Custom Root Supervisor

You can replace the default root supervisor with your own:

```csharp
services.AddTrupe(config =>
{
    config.SetRootSupervisor<MyCustomRootSupervisor>();
});
```

---

## DynamicSupervisor

A **DynamicSupervisor** allows you to add and remove child actors at runtime, after the supervisor has been initialized. It is ideal for scenarios where the set of actors is not known at startup.

### Key Features

- Children can be added and removed dynamically via messages.
- Always uses `Strategy.OneForOne` (sealed — cannot be changed).
- Supports `RemoveChild` messages for removing actors.

### Interface

```csharp
public abstract class DynamicSupervisor : Supervisor, IHandleActorMessage<RemoveChild>
{
    protected sealed override Strategy Strategy => Strategy.OneForOne;

    // Sends AddActor message asynchronously
    protected override IActorReference AddChild(IChildSpecification specification);

    protected override ValueTask<IActorReference> AddChildAsync(
        IChildSpecification specification, CancellationToken cancellationToken);

    // Remove a child actor
    protected virtual void RemoveActor(IActorReference reference);
    protected virtual ValueTask RemoveActorAsync(
        IActorReference reference, CancellationToken cancellationToken);
}
```

### Example

```csharp
public record StartSession(string SessionId);
public record EndSession(string SessionId);

public class SessionSupervisor : DynamicSupervisor,
    IHandleActorMessage<StartSession>,
    IHandleActorMessage<EndSession>
{
    private readonly Dictionary<string, IActorReference> _sessions = new();

    public SessionSupervisor(ILogger<SessionSupervisor> logger)
        : base(logger) { }

    public async ValueTask HandleAsync(StartSession message, CancellationToken cancellationToken = default)
    {
        var actorRef = await AddChildAsync<SessionActor>(cancellationToken);
        _sessions[message.SessionId] = actorRef;
    }

    public async ValueTask HandleAsync(EndSession message, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(message.SessionId, out var actorRef))
        {
            await RemoveActorAsync(actorRef, cancellationToken);
            _sessions.Remove(message.SessionId);
        }
    }
}
```

### When to Use

- The number of child actors changes at runtime (e.g., one actor per user session, connection, or job).
- You need to manage actor lifecycles on-demand.

---

## PartitionSupervisor\<TActor>

A **PartitionSupervisor** creates a fixed number of worker actors of the same type and routes messages to them. It is useful for distributing work across multiple instances of the same actor.

### Key Features

- Creates a configurable number of worker instances.
- Routes incoming messages to workers (partitioning logic).
- Supports `OneForOne` and `AllForOne` strategies.
- Configurable restart policy.

### Interface

```csharp
public abstract class PartitionSupervisor<TActor> : Actor, ISupervisor
{
    protected virtual int Workers { get; }
    protected virtual Strategy Strategy => Strategy.OneForOne;
    protected virtual RestartPolicy DefaultRestartPolicy => RestartPolicy.Permanent;
    protected virtual int MaxRestarts => 3;
    protected virtual TimeSpan RestartWindow => TimeSpan.FromSeconds(5);
}
```

### Example

```csharp
public class OrderProcessorPool : PartitionSupervisor<OrderProcessorActor>
{
    public OrderProcessorPool(ILogger<OrderProcessorPool> logger)
        : base(logger, workers: 4) { }
}
```

This creates 4 instances of `OrderProcessorActor` managed by the partition supervisor.

### When to Use

- You need to parallelize work across multiple instances of the same actor type.
- Load balancing or partitioning messages across workers.
- Fixed pool sizes where you control the concurrency level.

---

## Comparison

| Supervisor | Children | Strategy | Use Case |
|-----------|----------|----------|----------|
| **RootSupervisor** | Defined at startup | Always restart | Top-level system supervisor |
| **Supervisor** (base) | Defined at startup | Configurable | General-purpose supervision |
| **DynamicSupervisor** | Added/removed at runtime | One-for-One (fixed) | On-demand actor management |
| **PartitionSupervisor\<T>** | Fixed pool of same type | Configurable | Work distribution / pooling |

## Creating a Custom Supervisor

You can create your own supervisor by extending the `Supervisor` base class:

```csharp
public class MyCustomSupervisor : Supervisor
{
    public MyCustomSupervisor(ILogger<MyCustomSupervisor> logger)
        : base(logger) { }

    protected override Strategy Strategy => Strategy.OneForOne;
    protected override int MaxRestarts => 10;
    protected override TimeSpan RestartWindow => TimeSpan.FromMinutes(1);

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<DatabaseActor>();
        AddChild<CacheActor>();
        AddChild<ApiActor>();
        return ValueTask.CompletedTask;
    }

    protected override FailureAction GetFailureAction(Child child, Exception exception)
    {
        if (child.ActorType == typeof(DatabaseActor))
        {
            return exception is TimeoutException
                ? FailureAction.Restart
                : FailureAction.Escalate;
        }

        return FailureAction.Restart;
    }
}
```

## Next Steps

- [Supervision Strategies](supervision-strategies.md) — deep dive into One-for-One vs All-for-One.
- [Getting Started](../guides/getting-started.md) — set up a complete Trupe application.
