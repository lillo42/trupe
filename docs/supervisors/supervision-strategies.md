# Supervision Strategies

A supervision strategy determines how a supervisor responds when one of its child actors fails. Trupe supports two strategies: **One-for-One** and **All-for-One**.

## One-for-One Strategy

```
Strategy.OneForOne
```

When a child actor fails, **only that specific child** is affected by the failure action (restart, stop, etc.). All sibling actors continue running uninterrupted.

```
          ┌──────────────┐
          │  Supervisor  │
          └──────┬───────┘
                 │
     ┌───────────┼───────────┐
     │           │           │
     ▼           ▼           ▼
 ┌────────┐ ┌────────┐ ┌────────┐
 │Actor A │ │Actor B │ │Actor C │
 │  ✓ OK  │ │ ✗ FAIL │ │  ✓ OK  │
 └────────┘ └────┬───┘ └────────┘
                 │
                 ▼
            ┌─────────┐
            │Actor B  │  ← Only B is restarted
            │Restarted│
            └─────────┘
```

### When to Use

- Children are **independent** of each other.
- A failure in one child does not invalidate the state of siblings.
- This is the **default strategy** and the most commonly used.

### Example

```csharp
public class IndependentWorkersSupervisor : Supervisor
{
    public IndependentWorkersSupervisor(ILogger<IndependentWorkersSupervisor> logger)
        : base(logger) { }

    protected override Strategy Strategy => Strategy.OneForOne;

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<EmailWorker>();
        AddChild<SmsWorker>();
        AddChild<PushNotificationWorker>();
        return ValueTask.CompletedTask;
    }
}
```

If `EmailWorker` fails, only `EmailWorker` is restarted. `SmsWorker` and `PushNotificationWorker` keep running.

## All-for-One Strategy

```
Strategy.AllForOne
```

When a child actor fails, **all children** of the supervisor are affected by the failure action. If the action is restart, all children are restarted — not just the one that failed.

```
          ┌──────────────┐
          │  Supervisor  │
          └──────┬───────┘
                 │
     ┌───────────┼───────────┐
     │           │           │
     ▼           ▼           ▼
 ┌────────┐ ┌────────┐ ┌────────┐
 │Actor A │ │Actor B │ │Actor C │
 │  ✓ OK  │ │ ✗ FAIL │ │  ✓ OK  │
 └────┬───┘ └────┬───┘ └────┬───┘
      │          │          │
      ▼          ▼          ▼
 ┌─────────┐ ┌─────────┐ ┌─────────┐
 │Actor A  │ │Actor B  │ │Actor C  │  ← All restarted
 │Restarted│ │Restarted│ │Restarted│
 └─────────┘ └─────────┘ └─────────┘
```

### When to Use

- Children have **shared or dependent state** that must remain consistent.
- A failure in one child means siblings are likely in an invalid state too.
- You need a "clean slate" for the entire group of actors.

### Example

```csharp
public class PipelineSupervisor : Supervisor
{
    public PipelineSupervisor(ILogger<PipelineSupervisor> logger)
        : base(logger) { }

    protected override Strategy Strategy => Strategy.AllForOne;

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        AddChild<DataIngestionActor>();
        AddChild<DataTransformActor>();
        AddChild<DataOutputActor>();
        return ValueTask.CompletedTask;
    }
}
```

If `DataTransformActor` fails, all three actors in the pipeline are restarted to ensure the entire pipeline starts from a clean state.

## Strategy Comparison

| Feature | One-for-One | All-for-One |
|---------|-------------|-------------|
| Affected actors | Only the failed child | All children |
| Use case | Independent actors | Interdependent actors |
| Performance impact | Minimal | Higher (restarts all children) |
| State consistency | Per-actor | Group-wide |
| Default | ✓ Yes | No |

## Combining with Failure Actions

The strategy determines _which actors_ are affected. The failure action determines _what happens_ to them:

```csharp
public class MySupervisor : Supervisor
{
    public MySupervisor(ILogger<MySupervisor> logger)
        : base(logger) { }

    protected override Strategy Strategy => Strategy.AllForOne;
    protected override int MaxRestarts => 5;
    protected override TimeSpan RestartWindow => TimeSpan.FromSeconds(10);

    protected override FailureAction ResolveFailureAction(Child child, Exception exception)
    {
        return exception switch
        {
            // Transient errors: restart all children
            TimeoutException => FailureAction.Restart,
            HttpRequestException => FailureAction.Restart,

            // Fatal errors: stop all children
            InvalidConfigurationException => FailureAction.Stop,

            // Unknown: escalate to parent
            _ => FailureAction.Escalate
        };
    }
}
```

> **Note:** The base `ResolveFailureAction` only returns `Restart` or `Escalate`. If you want `Stop` or `Resume` for specific exception types, you must override it as shown above.

## Next Steps

- [Built-in Supervisors](built-in-supervisors.md) — explore `RootSupervisor`, `DynamicSupervisor`, and `PartitionSupervisor`.
- [What is a Supervisor?](what-is-a-supervisor.md) — revisit supervisor fundamentals.
