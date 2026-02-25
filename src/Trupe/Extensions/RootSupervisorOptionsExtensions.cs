using System;
using System.Diagnostics.CodeAnalysis;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Supervisors;

namespace Trupe.Extensions;

/// <summary>
/// Extension methods for configuring <see cref="RootSupervisorOptions"/> with child actors and supervisors.
/// </summary>
public static class RootSupervisorOptionsExtensions
{
    /// <summary>
    /// Adds a child actor of the specified type to the root supervisor options.
    /// </summary>
    /// <typeparam name="TActor">The type of the actor to add.</typeparam>
    /// <param name="options">The root supervisor options.</param>
    /// <param name="configure">An optional action to configure the child specification.</param>
    /// <returns>The <see cref="RootSupervisorOptions"/> for chaining.</returns>
    public static RootSupervisorOptions AddActor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActor
    >(this RootSupervisorOptions options, Action<ChildSpecification>? configure = null)
        where TActor : class, IActor
    {
        var childSpec = new ChildSpecification(typeof(TActor));
        configure?.Invoke(childSpec);
        options.Children.Add(childSpec);
        return options;
    }

    /// <summary>
    /// Adds a child actor of the specified type to the root supervisor options.
    /// </summary>
    /// <param name="options">The root supervisor options.</param>
    /// <param name="actorType">The type of the actor to add. Must implement <see cref="IActor"/>.</param>
    /// <param name="configure">An optional action to configure the child specification.</param>
    /// <returns>The <see cref="RootSupervisorOptions"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="actorType"/> does not implement <see cref="IActor"/>.</exception>
    public static RootSupervisorOptions AddActor(
        this RootSupervisorOptions options,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType,
        Action<ChildSpecification>? configure = null
    )
    {
        if (!actorType.IsActor())
        {
            throw new InvalidOperationException(
                $"Type {actorType.FullName} does not implement IActor."
            );
        }

        var childSpec = new ChildSpecification(actorType);
        configure?.Invoke(childSpec);
        options.Children.Add(childSpec);
        return options;
    }

    /// <summary>
    /// Adds a supervisor of the specified type to the root supervisor options.
    /// </summary>
    /// <typeparam name="TSupervisor">The type of the supervisor to add.</typeparam>
    /// <param name="options">The root supervisor options.</param>
    /// <returns>The <see cref="RootSupervisorOptions"/> for chaining.</returns>
    public static RootSupervisorOptions AddSupervisor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSupervisor
    >(this RootSupervisorOptions options)
        where TSupervisor : class, ISupervisor
    {
        return AddActor<TSupervisor>(options);
    }

    /// <summary>
    /// Adds a supervisor of the specified type to the root supervisor options.
    /// </summary>
    /// <param name="options">The root supervisor options.</param>
    /// <param name="supervisorType">The type of the supervisor to add. Must implement <see cref="ISupervisor"/>.</param>
    /// <returns>The <see cref="RootSupervisorOptions"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="supervisorType"/> does not implement <see cref="ISupervisor"/>.</exception>
    public static RootSupervisorOptions AddSupervisor(
        this RootSupervisorOptions options,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type supervisorType
    )
    {
        if (!supervisorType.IsSupervisor())
        {
            throw new InvalidOperationException(
                $"Type {supervisorType.FullName} does not implement ISupervisor."
            );
        }

        return AddActor(options, supervisorType);
    }
}
