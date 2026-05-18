# What is a Supervisor?

A **supervisor** is a specialized actor whose primary responsibility is to manage the lifecycle of its **child actors** and handle their failures. Supervisors form the backbone of Trupe's fault tolerance model.

## The "Let It Crash" Philosophy

In traditional programming, error handling is done defensively — wrapping every operation in try-catch blocks, validating every input, and hoping nothing falls through the cracks. This makes code complex and brittle.

The actor model takes a different approach: **let it crash**. Instead of trying to handle every possible error inside the actor, you let the actor fail and rely on its supervisor to decide what to do. This separates the _business logic_ (what the actor does) from the _error recovery logic_ (what happens when it fails).

```
                ┌────────────────────┐
                │   Root Supervisor  │
                └──────┬─────────────┘
                       │
            ┌──────────┼──────────┐
            │          │          │
            ▼          ▼          ▼
      ┌──────────┐ ┌──────────┐ ┌──────────┐
      │Supervisor│ │Supervisor│ │  Actor C │
      │    A     │ │    B     │ │          │
      └─────┬────┘ └─────┬────┘ └──────────┘
            │            │
        ┌───┴───┐    ┌───┴───┐
        │       │    │       │
        ▼       ▼    ▼       ▼
    ┌──────┐┌──────┐┌──────┐┌──────┐
    │Actor ││Actor ││Actor ││Actor │
    │  1   ││  2   ││  3   ││  4   │
    └──────┘└──────┘└──────┘└──────┘
```

This tree structure ensures that every actor has a supervisor. If a child fails, the supervisor handles it. If the supervisor itself fails, _its_ parent supervisor handles it, all the way up to the root supervisor.

## The `ISupervisor` Interface

```csharp
public interface ISupervisor : IActor
{
    IEnumerable<IActorReference> Children { get; }
}
```

A supervisor is an actor that also maintains a collection of child actor references.

## The `Supervisor` Base Class

The `Supervisor` base class provides the full supervision machinery:

```csharp
public abstract class Supervisor : Actor, ISupervisor
{
    // Strategy configuration
    protected virtual Strategy Strategy => Strategy.OneForOne;
    protected virtual int MaxRestarts => 3;
    protected virtual TimeSpan RestartWindow => TimeSpan.FromSeconds(5);

    // Child management
    protected virtual IActorReference AddChild<TActor>() where TActor : IActor;
    protected virtual IActorReference AddChild(Type actorType);
    protected virtual IActorReference AddChild(IChildSpecification specification);

    protected virtual ValueTask<IActorReference> AddChildAsync<TActor>(CancellationToken cancellationToken = default) where TActor : IActor;
    protected virtual ValueTask<IActorReference> AddChildAsync(Type actorType, CancellationToken cancellationToken = default);
    protected virtual ValueTask<IActorReference> AddChildAsync(IChildSpecification specification, CancellationToken cancellationToken = default);

    // Hooks
    protected virtual ValueTask OnInitializeAsync(CancellationToken cancellationToken = default);
    protected virtual FailureAction GetFailureAction(Child child, Exception exception);
}
```

### Key Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Strategy` | `OneForOne` | How failures affect sibling actors. |
| `MaxRestarts` | `3` | Maximum number of restarts allowed within the restart window. |
| `RestartWindow` | `5 seconds` | Time window for counting restarts. |

### Adding Children

Children are typically added during the `OnInitializeAsync` hook:

```csharp
public class TeamSupervisor : Supervisor
{
    public TeamSupervisor(ILogger<TeamSupervisor> logger)
        : base(logger) { }

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<WorkerActor>();
        AddChild<WorkerActor>();
        AddChild<LoggerActor>();
        return ValueTask.CompletedTask;
    }
}
```

You can also add children with custom specifications:

```csharp
var spec = new ChildSpecification(typeof(WorkerActor))
{
    Mailbox = new ChannelMailbox(maxSize: 100),
    RestartPolicy = RestartPolicy.Transient
};

AddChild(spec);
```

## Failure Handling Flow

When a child actor throws an exception, the following happens:

1. The actor process catches the exception.
2. An `ActorFailed` message is sent to the supervisor.
3. The supervisor calls `GetFailureAction()` to determine the response.
4. The supervisor applies the chosen action based on the configured `Strategy`.

### Failure Actions

| Action | Description |
|--------|-------------|
| `Restart` | The failed actor is stopped, a new instance is created, and processing resumes. Lifecycle hooks (`BeforeRestartAsync`, `AfterRestartAsync`) are called. |
| `Stop` | The actor is permanently terminated. |
| `Resume` | The actor continues processing the next message without restarting. |
| `Escalate` | The failure is passed to the supervisor's own supervisor. |

### Customizing Failure Handling

Override `GetFailureAction` to implement custom failure logic:

```csharp
protected override FailureAction GetFailureAction(Child child, Exception exception)
{
    return exception switch
    {
        TimeoutException => FailureAction.Restart,
        InvalidOperationException => FailureAction.Resume,
        OutOfMemoryException => FailureAction.Escalate,
        _ => FailureAction.Restart
    };
}
```

## Restart Policies

Each child can have a restart policy that determines whether it should be restarted after termination:

| Policy | Description |
|--------|-------------|
| `Permanent` | Always restart the child, regardless of why it stopped. |
| `Transient` | Only restart if the child stopped abnormally (due to an exception). |
| `Temporary` | Never restart. Once stopped, the child is gone. |

```csharp
var spec = new ChildSpecification(typeof(WorkerActor))
{
    RestartPolicy = RestartPolicy.Transient
};
```

## Restart Limits

Supervisors prevent infinite restart loops by tracking restart frequency:

- If a child is restarted more than `MaxRestarts` times within `RestartWindow`, the failure is **escalated** to the parent supervisor.
- This protects the system from a child that keeps crashing immediately after restart.

## Child Tracking

The supervisor maintains an immutable list of `Child` objects:

```csharp
public class Child
{
    public IActor Actor { get; set; }
    public IMailbox Mailbox { get; }
    public LocalActorReference Reference { get; }
    public RestartPolicy RestartPolicy { get; }
    public Type ActorType { get; }
    public int RestartCount { get; set; }
    public DateTimeOffset LastRestartTime { get; set; }
    public Dictionary<string, object> Metadata { get; }
    public bool IsSupervisor => Actor is ISupervisor;
}
```

## Next Steps

- [Supervision Strategies](supervision-strategies.md) — understand One-for-One vs All-for-One.
- [Built-in Supervisors](built-in-supervisors.md) — explore `RootSupervisor`, `DynamicSupervisor`, and `PartitionSupervisor`.
