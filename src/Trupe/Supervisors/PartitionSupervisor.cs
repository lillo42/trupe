using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Trupe.Abstractions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;
using Trupe.Mailboxes;

namespace Trupe.Supervisors;

/// <summary>
/// A supervisor that manages a fixed number of child actors of the same type,
/// routing messages to them using a partition key hash.
/// </summary>
/// <typeparam name="TActor">The type of actor managed by this supervisor.</typeparam>
/// <param name="logger">The logger instance for supervisor operations.</param>
/// <param name="workers">The number of worker actors to create.</param>
public abstract partial class PartitionSupervisor<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
        TActor
>(ILogger logger, int workers) : AbstractSupervisor(logger), ISupervisor, IAsyncDisposable
    where TActor : IActor
{
    /// <summary>
    /// Gets the number of worker actors managed by this supervisor.
    /// </summary>
    protected virtual int Workers { get; } = workers;

    /// <summary>
    /// Gets the default restart policy applied to child actors.
    /// Default is <see cref="RestartPolicy.Permanent"/>.
    /// </summary>
    protected virtual RestartPolicy DefaultRestartPolicy => RestartPolicy.Permanent;

    /// <summary>
    /// Initializes a new instance with the specified logger and a default worker count
    /// equal to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <param name="logger">The logger instance for supervisor operations.</param>
    public PartitionSupervisor(ILogger logger)
        : this(logger, Environment.ProcessorCount) { }

    /// <inheritdoc />
    /// <remarks>
    /// Creates and starts the configured number of worker actors during initialization.
    /// </remarks>
    public override async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        using (Logger.BeginScope("{SupervisorName}", Context.Name))
        {
            Log.InitializingPartitionSupervisor(Logger, typeof(TActor).Name, Workers);

            for (var i = 0; i < Workers; i++)
            {
                Log.CreatingWorkerActor(Logger, typeof(TActor).Name, i, Workers);

                var child = CreateActor(
                    new ChildSpecification(typeof(TActor))
                    {
                        RestartPolicy = DefaultRestartPolicy,
                        MailboxFactory = CreateMailbox,
                    }
                );

                await StartActorAsync(child);

                Children = Children.Add(child);

                Log.WorkerActorStarted(Logger, typeof(TActor).Name, i);
            }

            Log.PartitionSupervisorInitialized(Logger, typeof(TActor).Name, Workers);

            await base.InitializeAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gets the actor reference for a given partition key using consistent hashing.
    /// </summary>
    /// <typeparam name="TKey">The type of the partition key.</typeparam>
    /// <param name="key">The key used to determine which actor to route to.</param>
    /// <returns>The actor reference for the partition that handles the given key.</returns>
    protected virtual IActorReference GetActorReference<TKey>(TKey key)
        where TKey : notnull
    {
        var hash = Math.Abs(GetHashcode(key));
        var index = hash % Children.Count;

        Log.ResolvingPartition(Logger, typeof(TKey).Name, index, Children.Count);

        return Children[index].Reference;
    }

    /// <summary>
    /// Computes the hash code for a partition key. Override to customize partition distribution.
    /// </summary>
    /// <typeparam name="TKey">The type of the partition key.</typeparam>
    /// <param name="key">The key to hash.</param>
    /// <returns>The hash code for the key.</returns>
    protected virtual int GetHashcode<TKey>(TKey key)
        where TKey : notnull
    {
        return HashCode.Combine(key);
    }

    /// <inheritdoc />
    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask();
    }

    /// <summary>
    /// Creates a mailbox for a new worker actor.
    /// Override this method to use a custom mailbox implementation.
    /// </summary>
    /// <param name="provider">The service provider for resolving dependencies.</param>
    /// <returns>A new mailbox instance.</returns>
    protected virtual IMailbox CreateMailbox(IServiceProvider provider)
    {
        return new ChannelMailbox();
    }

    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Information,
            "Initializing partition supervisor for {ActorType} with {WorkerCount} workers"
        )]
        public static partial void InitializingPartitionSupervisor(
            ILogger logger,
            string actorType,
            int workerCount
        );

        [LoggerMessage(
            LogLevel.Debug,
            "Creating worker actor {WorkerIndex}/{WorkerCount} of type {ActorType}"
        )]
        public static partial void CreatingWorkerActor(
            ILogger logger,
            string actorType,
            int workerIndex,
            int workerCount
        );

        [LoggerMessage(LogLevel.Debug, "Worker actor {WorkerIndex} of type {ActorType} started")]
        public static partial void WorkerActorStarted(
            ILogger logger,
            string actorType,
            int workerIndex
        );

        [LoggerMessage(
            LogLevel.Information,
            "Partition supervisor initialized with {WorkerCount} {ActorType} workers"
        )]
        public static partial void PartitionSupervisorInitialized(
            ILogger logger,
            string actorType,
            int workerCount
        );

        [LoggerMessage(
            LogLevel.Debug,
            "Routing {KeyType} key to partition {PartitionIndex}/{PartitionCount}"
        )]
        public static partial void ResolvingPartition(
            ILogger logger,
            string keyType,
            int partitionIndex,
            int partitionCount
        );
    }
}
