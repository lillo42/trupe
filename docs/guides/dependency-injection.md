# Dependency Injection

Trupe integrates with `Microsoft.Extensions.DependencyInjection` out of the box. All actors, supervisors, and system components are resolved through the DI container.

## Registering the Actor System

Use the `AddTrupe` extension method on `IServiceCollection`:

```csharp
services.AddTrupe(config =>
{
    // Register actors
    config.AddActor<GreeterActor>();
    config.AddActor<CalculatorActor>();

    // Register supervisors
    config.AddSupervisor<AppSupervisor>();

    // Configure the root supervisor
    config.ConfigureRootSupervisor(options =>
    {
        // Root supervisor options
    });
});
```

## The `ActorSystemConfigurator`

The `ActorSystemConfigurator` provides the full configuration API:

| Method | Description |
|--------|-------------|
| `AddActor<TActor>(configure?)` | Registers an actor type with the DI container and optionally configures per-actor middlewares. |
| `AddActor(Type, configure?)` | Non-generic variant for runtime type registration. |
| `AddSupervisor<TSupervisor>(configure?)` | Registers a supervisor type with the DI container and optionally configures per-actor middlewares. |
| `AddSupervisor(Type, configure?)` | Non-generic variant for runtime type registration. |
| `Use<TMiddleware>(order?, metadata?)` | Adds a global pipeline middleware applied to all actors. |
| `AddMiddleware<TMiddleware>(...)` | Registers a middleware in DI (instance, factory, or type). |
| `ConfigureRootSupervisor(Action<RootSupervisorOptions>)` | Configures options for the root supervisor. |
| `SetRootSupervisor<TSupervisor>()` | Replaces the default root supervisor with a custom one. |
| `SetActorRegister(IActorRegister)` | Replaces the default actor registry. |
| `AddHostedService()` | Adds the hosted service for auto start/stop (requires `Trupe.Extensions.Hosting`). |

You can also access the underlying `IServiceCollection` directly:

```csharp
services.AddTrupe(config =>
{
    // Access the service collection for additional registrations
    config.Services.AddSingleton<IMyService, MyService>();
});
```

## Per-Message Scoped Services

Each message processed by an actor gets its own DI scope through `Context.ServiceProvider`. This allows you to resolve scoped services (such as `DbContext`) within a message handler:

```csharp
public class OrderActor : Actor, IHandleActorMessage<PlaceOrder>
{
    public async ValueTask HandleAsync(PlaceOrder message, CancellationToken cancellationToken = default)
    {
        await using var dbContext = Context.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Orders.Add(message.Order);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

The scope is automatically disposed after the message has been processed, so scoped services follow their expected lifetime.

## Actor Constructor Injection

Actors support standard constructor injection. Any service registered in the DI container can be injected:

```csharp
public class OrderActor : Actor, IHandleActorMessage<PlaceOrder>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderActor> _logger;
    private readonly IEmailService _emailService;

    public OrderActor(
        IOrderRepository repository,
        ILogger<OrderActor> logger,
        IEmailService emailService)
    {
        _repository = repository;
        _logger = logger;
        _emailService = emailService;
    }

    public async ValueTask HandleAsync(PlaceOrder message, CancellationToken cancellationToken = default)
    {
        await _repository.SaveAsync(message.Order, cancellationToken);
        await _emailService.SendConfirmationAsync(message.Order.Email, cancellationToken);
        _logger.LogInformation("Order {OrderId} placed.", message.Order.Id);
    }
}
```

## Supervisor Constructor Injection

Supervisors require `ILogger` in their constructor, plus any additional dependencies you need:

```csharp
public class DataPipelineSupervisor : Supervisor
{
    private readonly IConfiguration _config;

    public DataPipelineSupervisor(
        ILogger<DataPipelineSupervisor> logger,
        IConfiguration config)
        : base(logger)
    {
        _config = config;
    }

    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        var workerCount = _config.GetValue<int>("Pipeline:WorkerCount");
        for (var i = 0; i < workerCount; i++)
        {
            AddChild<PipelineWorkerActor>();
        }
        return ValueTask.CompletedTask;
    }
}
```

## The Actor Factory

Trupe uses `IActorFactory` to create actor instances. The default implementation (`DependencyInjectionActorFactory`) resolves actors from the DI container:

```csharp
public class DependencyInjectionActorFactory(IServiceProvider serviceProvider) : IActorFactory
{
    public IActor CreateActor(Type actorType)
        => (IActor)serviceProvider.GetRequiredService(actorType);
}
```

This means actors are created with full DI support, including scoped and transient dependencies.

## The Actor Registry

The `IActorRegister` is a thread-safe registry for looking up actors by name:

```csharp
public interface IActorRegister
{
    void Register(string id, IActorReference actor);
    bool TryRegister(string id, IActorReference actor);
    IActorReference? Get(string id);
    bool TryGet(string id, out IActorReference? actor);
    bool Contains(string id);
}
```

By default, Trupe uses a singleton `ActorRegister.Instance`. You can replace it with your own:

```csharp
services.AddTrupe(config =>
{
    config.SetActorRegister(new MyCustomActorRegister());
});
```

### Using the Registry

```csharp
var register = serviceProvider.GetRequiredService<IActorRegister>();

// Register an actor
register.Register("greeter", actorRef);

// Look up an actor
if (register.TryGet("greeter", out var greeterRef))
{
    greeterRef.Tell(new Greet("World"));
}
```

## Next Steps

- [Pipelines and Middleware](pipelines.md) — configure send/receive pipeline middlewares.
- [Hosting Integration](hosting-integration.md) — automatically manage the actor system lifecycle.
- [Getting Started](getting-started.md) — build a complete application.
