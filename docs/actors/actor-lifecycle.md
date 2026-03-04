# Actor Lifecycle

Every actor in Trupe goes through a well-defined lifecycle. Understanding these stages helps you manage resources, recover from failures, and build resilient systems.

## Lifecycle Stages

```
    ┌───────────────┐
    │  Created       │  Actor is instantiated by the factory
    └──────┬────────┘
           │
           ▼
    ┌───────────────┐
    │  Initialize    │  InitializeAsync() is called
    └──────┬────────┘
           │
           ▼
    ┌───────────────┐
    │  Running       │  Processing messages from the mailbox
    └──────┬────────┘
           │
      ┌────┴────┐
      │ Failure? │
      └────┬────┘
      Yes  │  No
      ▼    │
  ┌─────────┐  │
  │ Restart  │  │
  │ or Stop  │  │
  └────┬────┘  │
       │       │
       │       ▼
       │  ┌──────────┐
       │  │ Terminated│
       │  └──────────┘
       │
       ▼
  ┌──────────────────┐
  │ BeforeRestart     │  BeforeRestartAsync() - cleanup
  └──────┬───────────┘
         │
         ▼
  ┌──────────────────┐
  │ New Instance      │  Fresh actor is created
  └──────┬───────────┘
         │
         ▼
  ┌──────────────────┐
  │ AfterRestart      │  AfterRestartAsync() - re-initialization
  └──────┬───────────┘
         │
         ▼
  ┌──────────────────┐
  │ Running           │  Resumes processing messages
  └──────────────────┘
```

## Lifecycle Hooks

### `InitializeAsync`

Called once when the actor first starts, before it begins processing messages. Use it for setup work:

```csharp
public class DatabaseActor : Actor
{
    private DbConnection _connection = null!;

    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _connection = new SqlConnection("...");
        await _connection.OpenAsync(cancellationToken);
    }
}
```

### `BeforeRestartAsync`

Called before the actor is restarted due to a failure. Use it to clean up resources from the previous instance:

```csharp
public override async ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
{
    // Close connections, flush buffers, release resources
    if (_connection is not null)
    {
        await _connection.CloseAsync();
    }
}
```

### `AfterRestartAsync`

Called after a new instance of the actor has been created following a restart. Use it to re-initialize state:

```csharp
public override async ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
{
    // Re-open connections, reload state
    _connection = new SqlConnection("...");
    await _connection.OpenAsync(cancellationToken);
}
```

## Restart Behavior

When an actor fails (throws an exception during message processing), the supervisor decides what happens next based on its strategy and the configured failure action:

| Action | What Happens |
|--------|-------------|
| **Restart** | `BeforeRestartAsync` → new instance created → `AfterRestartAsync` → resume processing |
| **Stop** | Actor is terminated permanently. No restart hooks are called. |
| **Resume** | The actor continues processing the next message as if nothing happened. |
| **Escalate** | The failure is passed to the parent supervisor for handling. |

## Restart Limits

Supervisors track restart frequency to prevent infinite restart loops:

- **MaxRestarts** — maximum number of restarts allowed within the restart window (default: `3`).
- **RestartWindow** — the time window for counting restarts (default: `5 seconds`).

If an actor exceeds the restart limit, the supervisor escalates the failure to its parent.

## Complete Example

```csharp
public class ResilientWorker : Actor, IHandleActorMessage<ProcessJob>
{
    private HttpClient _client = null!;

    public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _client = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        return ValueTask.CompletedTask;
    }

    public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    public override ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
    {
        _client = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        return ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(ProcessJob message, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/jobs", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
```

## Next Steps

- [Mailboxes](mailboxes.md) — configure message queuing strategies.
- [Supervisors](../supervisors/what-is-a-supervisor.md) — learn how failures are handled.
