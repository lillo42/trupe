using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Extensions;

namespace Trupe.Configurators;

/// <summary>
/// Provides a fluent API for configuring the Trupe actor system, including registering actors,
/// supervisors, and the root supervisor within the dependency injection container.
/// </summary>
public class ActorSystemConfigurator
{
    private readonly IServiceCollection _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorSystemConfigurator"/> class and registers
    /// the default actor system services.
    /// </summary>
    /// <param name="serviceProvider">The service collection to register services with.</param>
    public ActorSystemConfigurator(IServiceCollection serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _serviceProvider.TryAddSingleton<ActorSystem>();
        _serviceProvider.TryAddSingleton<IRootSupervisor, RootSupervisor>();

        _serviceProvider.Configure<RootSupervisorOptions>(_ => { });
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
        _serviceProvider.TryAddTransient<TActor>();
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

        _serviceProvider.TryAddTransient(actorType);
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
        _serviceProvider.Configure(configure);
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
        _serviceProvider.AddSingleton<IRootSupervisor, TSupervisor>();
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

        _serviceProvider.AddSingleton(typeof(IRootSupervisor), rootSupervisorType);
        return this;
    }
}
