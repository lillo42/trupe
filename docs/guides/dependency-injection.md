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
| `SetActorRegistry(IActorProcessRegistry)` | Replaces the default actor process registry. |

The hosted service extension is provided by `Trupe.Extensions.Hosting`:

| Method | Description |
|--------|-------------|
| `AddHostedService()` (on `ActorSystemConfigurator`) | Adds the `ActorSystemHostedService` for auto start/stop. |
| `AddActorSystemHostedService()` (on `IServiceCollection`) | Same as above, registered separately. |

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

The scope is automatically disposed after the message has been processed, so scoped services follow their expected lifetime. System messages that implement `IUseSameActorScopeServiceMessage` — such as `InitializeActor` and `AfterRestartActor` — are processed in the actor's existing scope instead of a new per-message scope.

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

Trupe uses `IActorFactory` to create actor instances. The default implementation (`ActorFactory`) resolves actors from the DI container:

```csharp
public class ActorFactory(IServiceProvider serviceProvider) : IActorFactory
{
    public IActor CreateActor(Type actorType)
        => (IActor)serviceProvider.GetRequiredService(actorType);
}
```

This means actors are created with full DI support, including scoped and transient dependencies.

## The Actor Process Registry

The `IActorProcessRegistry` maps actor references to their running processes and resolves references by URI:

```csharp
public interface IActorProcessRegistry
{
    void Register(IActorReference reference, IActorProcess process);
    void UnRegister(IActorReference reference);
    IActorReference GetReference(Uri reference);
    IActorProcess GetProcess(IActorReference reference);
}
```

By default, Trupe uses `ActorProcessRegistry.Instance`. You can replace it with your own:

```csharp
services.AddTrupe(config =>
{
    config.SetActorRegistry(new MyCustomActorProcessRegistry());
});
```

### Using the Registry

```csharp
var registry = serviceProvider.GetRequiredService<IActorProcessRegistry>();

// Resolve a reference by URI
var greeterRef = registry.GetReference(new Uri("trupe://localhost/greeter"));

greeterRef.Tell(new Greet("World"));
```

For convenience, the `ActorReference` class wraps the registry so you can resolve by name:

```csharp
var greeterRef = new ActorReference("greeter", registry);
greeterRef.Tell(new Greet("World"));
```

## Next Steps

- [Pipelines and Middleware](pipelines.md) — configure send/receive pipeline middlewares.
- [Hosting Integration](hosting-integration.md) — automatically manage the actor system lifecycle.
- [Getting Started](getting-started.md) — build a complete application.
