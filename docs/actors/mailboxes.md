# Mailboxes

Every actor in Trupe has a **mailbox** — a message queue that stores incoming messages until the actor is ready to process them. Trupe uses `System.Threading.Channels` under the hood for high-performance, thread-safe message delivery.

## How Mailboxes Work

```
Sender A ──Tell──►┐
                   │
Sender B ──Tell──►─┤  ┌──────────────────────────┐     ┌───────────┐
                   ├──►  Mailbox (Message Queue)  ├────►│   Actor   │
Sender C ──Ask───►─┤  └──────────────────────────┘     └───────────┘
                   │
Sender D ──Tell──►┘
```

- Multiple senders can write to the mailbox concurrently (thread-safe).
- The actor reads messages one at a time, in order.
- If the mailbox is bounded, senders may block or drop messages based on the configured policy.

## The `IMailbox` Interface

```csharp
public interface IMailbox : IAsyncEnumerable<IMessage>
{
    ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken = default);
    ValueTask CleanAsync();
}
```

| Method | Description |
|--------|-------------|
| `EnqueueAsync` | Adds a message to the mailbox. |
| `CleanAsync` | Clears all messages and resets the mailbox (used during actor restarts). |

The mailbox implements `IAsyncEnumerable<IMessage>`, allowing the actor process to consume messages asynchronously.

## ChannelMailbox

`ChannelMailbox` is the default mailbox implementation, backed by `System.Threading.Channels`:

### Unbounded Mailbox (Default)

An unbounded mailbox has no capacity limit. Messages are always accepted:

```csharp
var mailbox = new ChannelMailbox();
```

This is the default when no mailbox is specified. It is suitable for most use cases, but be aware that a slow consumer with a fast producer can lead to unbounded memory growth.

### Bounded Mailbox

A bounded mailbox has a maximum capacity. When the mailbox is full, the behavior depends on the configured `BoundedChannelFullMode`:

```csharp
// Bounded mailbox with capacity of 100 messages
var mailbox = new ChannelMailbox(maxSize: 100);
```

### Bounded Mailbox with Custom Full Behavior

```csharp
var mailbox = new ChannelMailbox(
    maxSize: 100,
    fullMode: BoundedChannelFullMode.DropOldest
);
```

| Full Mode | Behavior |
|-----------|----------|
| `Wait` (default) | The sender blocks until space is available. |
| `DropNewest` | The newest message in the queue is dropped to make room. |
| `DropOldest` | The oldest message in the queue is dropped to make room. |
| `DropWrite` | The message being written is dropped. |

## Configuring Mailboxes per Actor

You can specify a custom mailbox when adding a child actor to a supervisor using `ChildSpecification`:

```csharp
protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
{
    var spec = new ChildSpecification(typeof(WorkerActor))
    {
        Mailbox = new ChannelMailbox(maxSize: 50, fullMode: BoundedChannelFullMode.DropOldest),
        RestartPolicy = RestartPolicy.Permanent
    };

    AddChild(spec);
    return ValueTask.CompletedTask;
}
```

## When to Use Bounded Mailboxes

| Scenario | Recommendation |
|----------|---------------|
| Most applications | **Unbounded** — simpler, no risk of message loss. |
| High-throughput producers | **Bounded with `Wait`** — applies backpressure to slow down producers. |
| Lossy/real-time data | **Bounded with `DropOldest`** — always process the latest data. |
| Load shedding | **Bounded with `DropWrite`** — reject new messages when overwhelmed. |

## Next Steps

- [Creating an Actor](creating-an-actor.md) — build your first actor.
- [Supervisors](../supervisors/what-is-a-supervisor.md) — learn about fault tolerance.
