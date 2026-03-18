using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.SystemMessages;
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Messages;

namespace Trupe;

/// <summary>
/// Manages the lifecycle of the actor system, including starting and stopping the root supervisor.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ActorSystem"/> class.
/// </remarks>
/// <param name="rootSupervisor">The root supervisor that manages the actor hierarchy.</param>
/// <param name="serviceProvider">The service provider used to create DI scopes for actors.</param>
public class ActorSystem(IRootSupervisor rootSupervisor, IServiceProvider serviceProvider)
{
    private ActorProcess? _process;
    private readonly IRootSupervisor _rootSupervisor = rootSupervisor;

    /// <summary>
    /// Starts the actor system by initializing the root supervisor and beginning message processing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the actor system is already running.</exception>
    public void Start()
    {
        if (_process != null)
        {
            throw new InvalidOperationException("Actor system is already running.");
        }

        var mailbox = new ChannelMailbox();

        _rootSupervisor.Context = new ActorContext(
            new LocalActorReference(mailbox),
            serviceProvider.CreateAsyncScope()
        );

        _process = new ActorProcess(_rootSupervisor, mailbox);
        _process.Start(new LocalTellMessage(new InitializeActor()));
    }

    /// <summary>
    /// Gracefully stops the actor system and waits for all actors to finish processing.
    /// </summary>
    /// <returns>A task that completes when the actor system has stopped.</returns>
    public async Task StopAsync()
    {
        if (_process != null)
        {
            await _process.StopAsync();
            _process = null;
        }
    }
}
