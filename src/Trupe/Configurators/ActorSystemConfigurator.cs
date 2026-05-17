using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;
using Trupe.Extensions;
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
        _serviceCollection.TryAddSingleton(_ => ActorRegister.Instance);

        _serviceCollection.TryAddTransient<IReceivePipelineFactory, ReceivePipelineFactory>();

        _serviceCollection.TryAddTransient<ISendPipelineFactory, SendPipelineFactory>();

        _serviceCollection.TryAddSingleton<IActorFactory, ActorFactory>();

        _serviceCollection.TryAddSingleton<AskMiddleware>();
        _serviceCollection.TryAddSingleton<ActorMessageDispatcherMiddleware>();
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
        });

        _serviceCollection.Configure<RootSupervisorOptions>(_ => { });
    }

    /// <summary>
    /// Registers an actor type with the dependency injection container as a transient service.
    /// </summary>
    /// <typeparam name="TActor">The type of the actor to register.</typeparam>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator AddActor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
    >()
        where TActor : class, IActor
    {
        _serviceCollection.TryAddTransient<TActor>();
        return this;
    }

    /// <summary>
    /// Registers an actor type with the dependency injection container as a transient service.
    /// </summary>
    /// <param name="actorType">The type of the actor to register. Must implement <see cref="IActor"/>.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="actorType"/> does not implement <see cref="IActor"/>.</exception>
    public ActorSystemConfigurator AddActor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
    {
        if (!actorType.IsActor())
        {
            throw new InvalidOperationException(
                $"Type {actorType.FullName} does not implement IActor."
            );
        }

        _serviceCollection.TryAddTransient(actorType);
        return this;
    }

    /// <summary>
    /// Registers a supervisor type with the dependency injection container as a transient service.
    /// </summary>
    /// <typeparam name="TSupervisor">The type of the supervisor to register.</typeparam>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator AddSupervisor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSupervisor
    >()
        where TSupervisor : class, ISupervisor
    {
        return AddActor<TSupervisor>();
    }

    /// <summary>
    /// Registers a supervisor type with the dependency injection container as a transient service.
    /// </summary>
    /// <param name="supervisorType">The type of the supervisor to register. Must implement <see cref="ISupervisor"/>.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="supervisorType"/> does not implement <see cref="ISupervisor"/>.</exception>
    public ActorSystemConfigurator AddSupervisor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type supervisorType
    )
    {
        if (supervisorType.IsSupervisor())
        {
            throw new InvalidOperationException(
                $"Type {supervisorType.FullName} does not implement ISupervisor."
            );
        }

        return AddActor(supervisorType);
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
        if (rootSupervisorType.IsRootSupervisor())
        {
            throw new InvalidOperationException(
                $"Type {rootSupervisorType.FullName} does not implement ISupervisor."
            );
        }

        _serviceCollection.AddSingleton(typeof(IRootSupervisor), rootSupervisorType);
        return this;
    }

    /// <summary>
    /// Sets a custom <see cref="IActorRegister"/> instance, replacing the default <see cref="ActorRegister.Instance"/>.
    /// </summary>
    /// <param name="actorRegister">The <see cref="IActorRegister"/> instance to use.</param>
    /// <returns>The <see cref="ActorSystemConfigurator"/> for chaining.</returns>
    public ActorSystemConfigurator SetActorRegister(IActorRegister actorRegister)
    {
        _serviceCollection.AddSingleton(_ => actorRegister);
        return this;
    }

    public ActorSystemConfigurator AddMiddleware<TMiddleware>(TMiddleware middleware)
        where TMiddleware : class, IMiddleware
    {
        _serviceCollection.TryAddSingleton(middleware);
        return this;
    }

    public ActorSystemConfigurator AddMiddleware<TMiddleware>(
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

    public ActorSystemConfigurator AddMiddleware(IMiddleware middleware)
    {
        _serviceCollection.TryAdd(new ServiceDescriptor(middleware.GetType(), middleware));
        return this;
    }

    public ActorSystemConfigurator AddMiddleware(
        Type middlewareType,
        ServiceLifetime lifetime = ServiceLifetime.Transient
    )
    {
        _serviceCollection.TryAdd(new ServiceDescriptor(middlewareType, middlewareType, lifetime));
        return this;
    }

    public ActorSystemConfigurator AddMiddleware(
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

    public ActorSystemConfigurator Use<TMiddleware>()
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, null);
    }

    public ActorSystemConfigurator Use<TMiddleware>(int order)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(order, null);
    }

    public ActorSystemConfigurator Use<TMiddleware>(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, metadata);
    }

    public ActorSystemConfigurator Use<TMiddleware>(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use(typeof(TMiddleware), order, metadata);
    }

    public ActorSystemConfigurator Use(Type middlewareType)
    {
        return Use(middlewareType, 0, null);
    }

    public ActorSystemConfigurator Use(Type middlewareType, int order)
    {
        return Use(middlewareType, order, null);
    }

    public ActorSystemConfigurator Use(Type middlewareType, object? metadata)
    {
        return Use(middlewareType, 0, metadata);
    }

    public ActorSystemConfigurator Use(Type middlewareType, int order, object? metadata)
    {
        _serviceCollection.TryAddTransient(middlewareType);
        _serviceCollection.Configure<PipelineOptions>(opt =>
            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = order,
                    Metadata = metadata,
                    MiddlewareType = middlewareType,
                }
            )
        );

        return this;
    }
}
