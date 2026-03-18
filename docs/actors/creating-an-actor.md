# Creating an Actor

This guide walks you through creating actors in Trupe, from simple message handlers to actors that support multiple message types and request-response patterns.

## Defining Messages

Messages are simple .NET objects. We recommend using **records** for their immutability and concise syntax:

```csharp
public record Greet(string Name);
public record Add(int A, int B);
public record UserCreated(string Id, string Email);
```

Messages should be:

- **Immutable** — use records or read-only classes.
- **Self-contained** — include all data the actor needs to process the message.
- **Serializable** — if you plan to use remote actors in the future.

## A Simple Actor

The simplest actor handles a single message type:

```csharp
using Trupe;

public record Greet(string Name);

public class GreeterActor : Actor, IHandleActorMessage<Greet>
{
    public ValueTask HandleAsync(Greet message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello, {message.Name}!");
        return ValueTask.CompletedTask;
    }
}
```

Key points:

- Extend `Actor` for the base class functionality.
- Implement `IHandleActorMessage<Greet>` for type-safe message handling.
- The `HandleAsync` method is called whenever a `Greet` message is received.

## Handling Multiple Message Types

An actor can handle as many message types as needed by implementing multiple `IHandleActorMessage<T>` interfaces:

```csharp
public record CreateUser(string Name, string Email);
public record DeleteUser(string Id);
public record GetUser(string Id);

public class UserActor : Actor,
    IHandleActorMessage<CreateUser>,
    IHandleActorMessage<DeleteUser>,
    IHandleActorMessage<GetUser>
{
    private readonly Dictionary<string, (string Name, string Email)> _users = new();

    public ValueTask HandleAsync(CreateUser message, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString();
        _users[id] = (message.Name, message.Email);
        Console.WriteLine($"User {id} created.");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(DeleteUser message, CancellationToken cancellationToken = default)
    {
        _users.Remove(message.Id);
        Console.WriteLine($"User {message.Id} deleted.");
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GetUser message, CancellationToken cancellationToken = default)
    {
        if (_users.TryGetValue(message.Id, out var user))
        {
            Context.Response = user;
        }

        return ValueTask.CompletedTask;
    }
}
```

Since actors process messages sequentially, the `_users` dictionary does not need any synchronization — only one message is processed at a time.

## Request-Response Pattern (Ask)

When you need to get a value back from an actor, use the **Ask** pattern:

**Actor side** — set `Context.Response`:

```csharp
public record Add(int A, int B);

public class CalculatorActor : Actor, IHandleActorMessage<Add>
{
    public ValueTask HandleAsync(Add message, CancellationToken cancellationToken = default)
    {
        Context.Response = message.A + message.B;
        return ValueTask.CompletedTask;
    }
}
```

**Caller side** — use `AskAsync<TResponse>`:

```csharp
var result = await calculatorRef.AskAsync<int>(new Add(2, 3));
Console.WriteLine(result); // 5
```

Or the synchronous version with a timeout:

```csharp
var result = calculatorRef.Ask<int>(new Add(2, 3), timeout: TimeSpan.FromSeconds(5));
```

## Fire-and-Forget Pattern (Tell)

When you don't need a response, use **Tell**:

```csharp
greeterRef.Tell(new Greet("World"));

// Or async
await greeterRef.TellAsync(new Greet("World"));
```

`Tell` is non-blocking — the message is enqueued in the actor's mailbox and the caller continues immediately.

All `Tell` and `Ask` methods accept an optional `metadata` parameter of type `Dictionary<string, object>?` for attaching contextual information (e.g., correlation IDs) without modifying your message types.

## Using Dependency Injection

Actors support constructor injection. Register dependencies in the DI container and they will be resolved when the actor is created:

```csharp
public class OrderActor : Actor, IHandleActorMessage<PlaceOrder>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderActor> _logger;

    public OrderActor(IOrderRepository repository, ILogger<OrderActor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask HandleAsync(PlaceOrder message, CancellationToken cancellationToken = default)
    {
        await _repository.SaveAsync(message.Order, cancellationToken);
        _logger.LogInformation("Order {OrderId} placed.", message.Order.Id);
    }
}
```

Register the actor and its dependencies:

```csharp
services.AddTrupe(config =>
{
    config.AddActor<OrderActor>();
});
```

## Initialization

Override `InitializeAsync` to perform setup work when the actor starts:

```csharp
public class CacheActor : Actor, IHandleActorMessage<GetValue>
{
    private Dictionary<string, string> _cache = new();

    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Load initial data
        _cache = await LoadCacheFromDatabaseAsync(cancellationToken);
    }

    public ValueTask HandleAsync(GetValue message, CancellationToken cancellationToken = default)
    {
        Context.Response = _cache.GetValueOrDefault(message.Key);
        return ValueTask.CompletedTask;
    }
}
```

## Next Steps

- [Actor Lifecycle](actor-lifecycle.md) — learn about restart and cleanup hooks.
- [Mailboxes](mailboxes.md) — configure how messages are queued and delivered.
- [Supervisors](../supervisors/what-is-a-supervisor.md) — handle failures automatically.
