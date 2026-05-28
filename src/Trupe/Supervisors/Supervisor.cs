using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Supervisors;

/// <summary>
/// Abstract base class for preemptive supervisors that define their children during initialization.
/// Children are added before initialization completes and cannot be added afterward.
/// </summary>
/// <param name="logger">The logger instance for supervisor operations.</param>
public abstract partial class Supervisor(ILogger logger)
    : AbstractSupervisor(logger),
        ISupervisor,
        IHandleActorMessage<AddActor>,
        IAsyncDisposable
{
    private bool _initialized;

    /// <summary>
    /// Gets a value indicating whether the supervisor has completed initialization.
    /// After initialization, child actors cannot be added synchronously.
    /// </summary>
    protected virtual bool Initialized => _initialized;

    /// <inheritdoc />
    /// <remarks>
    /// Calls <see cref="AbstractSupervisor.InitializeAsync"/> and marks the supervisor as initialized.
    /// After initialization, <see cref="AddChild{TActor}()"/> will throw if called.
    /// </remarks>
    public sealed override async ValueTask InitializeAsync(
        CancellationToken cancellationToken = default
    )
    {
        await base.InitializeAsync(cancellationToken);

        _initialized = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Routes <see cref="AddActor"/> messages to the typed handler before falling back to the base implementation.
    /// </remarks>
    public override ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        if (message is AddActor addActor)
        {
            return HandleAsync(addActor, cancellationToken);
        }
        else
        {
            return base.HandleAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Handles the <see cref="AddActor"/> command by adding the child to the children list
    /// and starting its process.
    /// </summary>
    /// <param name="message">The command containing the child actor to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public virtual async ValueTask HandleAsync(
        AddActor message,
        CancellationToken cancellationToken
    )
    {
        Children = Children.Add(message.Child);
        await StartActorAsync(message.Child);
    }

    /// <summary>
    /// Adds a child actor of the specified type with default configuration.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            TActor
    >()
        where TActor : IActor
    {
        return AddChild(typeof(TActor));
    }

    /// <summary>
    /// Adds a child actor of the specified type with default configuration.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            Type actorType
    )
    {
        return AddChild(new ChildSpecification(actorType));
    }

    /// <summary>
    /// Adds a child actor using the specified specification.
    /// </summary>
    /// <param name="specification">The specification defining the child actor to create.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual IActorReference AddChild(IChildSpecification specification)
    {
        if (Initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var child = CreateActor(specification);
        var registry = Context.ServiceProvider.GetRequiredService<IActorProcessRegistry>();

        Context.Self.Tell(new AddActor(child));

        return new ActorReference(specification.Name, registry);
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with default configuration.
    /// </summary>
    /// <typeparam name="TActor">The type of actor to create.</typeparam>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            TActor
    >(CancellationToken cancellationToken = default)
        where TActor : IActor
    {
        return AddChildAsync(typeof(TActor), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor of the specified type with default configuration.
    /// </summary>
    /// <param name="actorType">The type of actor to create.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            Type actorType,
        CancellationToken cancellationToken = default
    )
    {
        return AddChildAsync(new ChildSpecification(actorType), cancellationToken);
    }

    /// <summary>
    /// Asynchronously adds a child actor using the specified specification.
    /// </summary>
    /// <param name="specification">The specification defining the child actor to create.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A reference to the newly created child actor.</returns>
    /// <exception cref="SupervisorAlreadyInitializedException">
    /// Thrown if called after the supervisor has been initialized.
    /// </exception>
    protected virtual ValueTask<IActorReference> AddChildAsync(
        IChildSpecification specification,
        CancellationToken cancellationToken = default
    )
    {
        if (Initialized)
        {
            throw new SupervisorAlreadyInitializedException(
                "Supervisor already initialized, it's preemptive"
            );
        }

        var child = CreateActor(specification);

        var registry = Context.ServiceProvider.GetRequiredService<IActorProcessRegistry>();
        var actorRef = new ActorReference(specification.Name, registry);

        var val = Context.Self.TellAsync(new AddActor(child), cancellationToken);

        if (val.IsCompletedSuccessfully)
        {
            return new ValueTask<IActorReference>(actorRef);
        }
        else
        {
            return new ValueTask<IActorReference>(AwaitAddChildAsync(val.AsTask(), actorRef));
        }

        static async Task<IActorReference> AwaitAddChildAsync(Task val, IActorReference actorRef)
        {
            await val;
            return actorRef;
        }
    }
}
