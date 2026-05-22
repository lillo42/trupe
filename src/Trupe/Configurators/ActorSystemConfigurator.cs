using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trupe.Abstractions;
using Trupe.Abstractions.Extensions;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;
using Trupe.Factories;
using Trupe.Pipelines;
using Trupe.Pipelines.Middlewares;

namespace Trupe.Configurators;

/// <summary>
/// Provides a fluent API for configuring the Trupe actor system, including registering actors,
/// supervisors, and the root supervisor within the dependency injection container.
/// </summary>
public class ActorSystemConfigurator
{
    private readonly IServiceCollection _serviceCollection;

    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/> used to register services.
    /// </summary>
    public IServiceCollection Services => _serviceCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorSystemConfigurator"/> class and registers
    /// the default actor system services.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register services with.</param>
    public ActorSystemConfigurator(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;

        _serviceCollection.TryAddSingleton<ActorSystem>();
        _serviceCollection.TryAddSingleton<IRootSupervisor, RootSupervisor>();
        _serviceCollection.TryAddSingleton(_ => ActorProcessRegistry.Instance);

        _serviceCollection.TryAddSingleton<IPipelineLookup, PipelineRegistry>();

        _serviceCollection.TryAddTransient<IReceivePipelineFactory, ReceivePipelineFactory>();
        _serviceCollection.TryAddSingleton<
            IReceivePipelineContextFactory,
            ReceivePipelineContextFactory
        >();
        _serviceCollection.TryAddSingleton<SettableReceivePipelineContextAccessor>();
        _serviceCollection.TryAddSingleton<IReceivePipelineContextAccessor>(provider =>
            provider.GetRequiredService<SettableReceivePipelineContextAccessor>()
        );

        _serviceCollection.TryAddTransient<ISendPipelineFactory, SendPipelineFactory>();
        _serviceCollection.TryAddSingleton<
            ISendPipelineContextFactory,
            SendPipelineContextFactory
        >();
        _serviceCollection.TryAddSingleton<SettableSendPipelineContextAccessor>();
        _serviceCollection.TryAddSingleton<ISendPipelineContextAccessor>(provider =>
            provider.GetRequiredService<SettableSendPipelineContextAccessor>()
        );

        _serviceCollection.TryAddSingleton<IActorFactory, ActorFactory>();

        _serviceCollection.TryAddSingleton<AskMiddleware>();
        _serviceCollection.TryAddSingleton<ActorMessageDispatcherMiddleware>();
        _serviceCollection.TryAddSingleton<ActorProcessDispatcherMiddleware>();
        _serviceCollection.Configure<PipelineOptions>(static opt =>
        {
            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = int.MinValue,
                    MiddlewareType = typeof(AskMiddleware),
                }
            );

            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = int.MaxValue,
                    MiddlewareType = typeof(ActorMessageDispatcherMiddleware),
                }
            );

            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = int.MaxValue,
                    MiddlewareType = typeof(ActorProcessDispatcherMiddleware),
                }
            );
        });

        _serviceCollection.Configure<RootSupervisorOptions>(_ => { });
    }

    /// <summary>
    /// Registers an actor type with the dependency injection container as a transient service
    /// and optionally configures its pipeline middlewares.
    /// </summary>
    /// <typeparam name="TActor">The type of the actor to register.</typeparam>
    /// <param name="configure">An optional action to configure per-actor pipeline middlewares.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator AddActor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
    >(Action<ActorConfigurator>? configure = null)
        where TActor : class, IActor
    {
        _serviceCollection.TryAddTransient<TActor>();
        configure?.Invoke(new ActorConfigurator(_serviceCollection, typeof(TActor)));
        return this;
    }

    /// <summary>
    /// Registers an actor type with the dependency injection container as a transient service
    /// and optionally configures its pipeline middlewares.
    /// </summary>
    /// <param name="actorType">The type of the actor to register. Must implement <see cref="IActor"/>.</param>
    /// <param name="configure">An optional action to configure per-actor pipeline middlewares.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="actorType"/> does not implement <see cref="IActor"/>.</exception>
    public ActorSystemConfigurator AddActor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType,
        Action<ActorConfigurator>? configure = null
    )
    {
        if (!actorType.IsActor())
        {
            throw new InvalidOperationException(
                $"Type {actorType.FullName} does not implement IActor."
            );
        }

        _serviceCollection.TryAddTransient(actorType);
        configure?.Invoke(new ActorConfigurator(_serviceCollection, actorType));
        return this;
    }

    /// <summary>
    /// Registers a supervisor type with the dependency injection container as a transient service
    /// and optionally configures its pipeline middlewares.
    /// </summary>
    /// <typeparam name="TSupervisor">The type of the supervisor to register.</typeparam>
    /// <param name="configure">An optional action to configure per-actor pipeline middlewares.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator AddSupervisor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSupervisor
    >(Action<ActorConfigurator>? configure = null)
        where TSupervisor : class, ISupervisor
    {
        return AddActor<TSupervisor>(configure);
    }

    /// <summary>
    /// Registers a supervisor type with the dependency injection container as a transient service
    /// and optionally configures its pipeline middlewares.
    /// </summary>
    /// <param name="supervisorType">The type of the supervisor to register. Must implement <see cref="ISupervisor"/>.</param>
    /// <param name="configure">An optional action to configure per-actor pipeline middlewares.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="supervisorType"/> does not implement <see cref="ISupervisor"/>.</exception>
    public ActorSystemConfigurator AddSupervisor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type supervisorType,
        Action<ActorConfigurator>? configure = null
    )
    {
        if (!supervisorType.IsSupervisor())
        {
            throw new InvalidOperationException(
                $"Type {supervisorType.FullName} does not implement ISupervisor."
            );
        }

        return AddActor(supervisorType, configure);
    }

    /// <summary>
    /// Configures the <see cref="RootSupervisorOptions"/> for the root supervisor.
    /// </summary>
    /// <param name="configure">An action to configure the root supervisor options.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator ConfigureRootSupervisor(Action<RootSupervisorOptions> configure)
    {
        _serviceCollection.Configure(configure);
        return this;
    }

    /// <summary>
    /// Sets a custom root supervisor type, replacing the default <see cref="RootSupervisor"/>.
    /// </summary>
    /// <typeparam name="TSupervisor">The type of the root supervisor.</typeparam>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator SetRootSupervisor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSupervisor
    >()
        where TSupervisor : class, IRootSupervisor
    {
        _serviceCollection.AddSingleton<IRootSupervisor, TSupervisor>();
        return this;
    }

    /// <summary>
    /// Sets a custom root supervisor type, replacing the default <see cref="RootSupervisor"/>.
    /// </summary>
    /// <param name="rootSupervisorType">The type of the root supervisor. Must implement <see cref="IRootSupervisor"/>.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="rootSupervisorType"/> does not implement <see cref="IRootSupervisor"/>.</exception>
    public ActorSystemConfigurator SetRootSupervisor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type rootSupervisorType
    )
    {
        if (!rootSupervisorType.IsRootSupervisor())
        {
            throw new InvalidOperationException(
                $"Type {rootSupervisorType.FullName} does not implement ISupervisor."
            );
        }

        _serviceCollection.AddSingleton(typeof(IRootSupervisor), rootSupervisorType);
        return this;
    }

    public ActorSystemConfigurator SetActorRegistery(IActorProcessRegistry actorRegistery)
    {
        _serviceCollection.AddSingleton(_ => actorRegistery);
        return this;
    }

    /// <summary>
    /// Registers a middleware singleton instance.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="middleware">The middleware instance to register.</param>
    public ActorSystemConfigurator AddMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >(TMiddleware middleware)
        where TMiddleware : class, IMiddleware
    {
        _serviceCollection.TryAddSingleton(middleware);
        return this;
    }

    /// <summary>
    /// Registers a middleware using a factory delegate with the specified lifetime.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="middlewareFactory">The factory delegate to create middleware instances.</param>
    /// <param name="lifetime">The service lifetime for the middleware registration.</param>
    public ActorSystemConfigurator AddMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >(
        Func<IServiceProvider, TMiddleware> middlewareFactory,
        ServiceLifetime lifetime = ServiceLifetime.Transient
    )
        where TMiddleware : class, IMiddleware
    {
        _serviceCollection.TryAdd(
            new ServiceDescriptor(typeof(TMiddleware), middlewareFactory, lifetime)
        );

        return this;
    }

    /// <summary>
    /// Registers a middleware singleton instance by its runtime type.
    /// </summary>
    /// <param name="middleware">The middleware instance to register.</param>
    public ActorSystemConfigurator AddMiddleware(IMiddleware middleware)
    {
        _serviceCollection.TryAdd(new ServiceDescriptor(middleware.GetType(), middleware));
        return this;
    }

    /// <summary>
    /// Registers a middleware by type with the specified lifetime.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="lifetime">The service lifetime for the middleware registration.</param>
    public ActorSystemConfigurator AddMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type middlewareType,
        ServiceLifetime lifetime = ServiceLifetime.Transient
    )
    {
        _serviceCollection.TryAdd(new ServiceDescriptor(middlewareType, middlewareType, lifetime));
        return this;
    }

    /// <summary>
    /// Registers a middleware by type using a factory delegate with the specified lifetime.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="middlewareFactory">The factory delegate to create middleware instances.</param>
    /// <param name="lifetime">The service lifetime for the middleware registration.</param>
    public ActorSystemConfigurator AddMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type middlewareType,
        Func<IServiceProvider, object> middlewareFactory,
        ServiceLifetime lifetime = ServiceLifetime.Transient
    )
    {
        _serviceCollection.TryAdd(
            new ServiceDescriptor(middlewareType, middlewareFactory, lifetime)
        );
        return this;
    }

    /// <summary>
    /// Adds a global middleware of type <typeparamref name="TMiddleware"/> to the pipeline with default order and no metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    public ActorSystemConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >()
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, null);
    }

    /// <summary>
    /// Adds a global middleware of type <typeparamref name="TMiddleware"/> with the specified execution order.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    public ActorSystemConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >(int order)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(order, null);
    }

    /// <summary>
    /// Adds a global middleware of type <typeparamref name="TMiddleware"/> with the specified metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorSystemConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, metadata);
    }

    /// <summary>
    /// Adds a global middleware of type <typeparamref name="TMiddleware"/> with the specified order and metadata,
    /// and registers it in the DI container as transient.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorSystemConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware
    >(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        _serviceCollection.TryAddTransient<TMiddleware>();
        _serviceCollection.Configure<PipelineOptions>(opt =>
            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = order,
                    Metadata = metadata,
                    MiddlewareType = typeof(TMiddleware),
                }
            )
        );

        return this;
    }
}
