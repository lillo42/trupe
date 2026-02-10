using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Trupe.ActorReferences;
using Trupe.Exceptions;
using Trupe.Factories;
using Trupe.Mailboxes;
using Trupe.Supervisors;

namespace Trupe.Tests.Supervisors;

public class PartitionSupervisorTest
{
    [Test]
    public async Task ChildrenCount_Should_BeCorrectlyAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();
        const int workers = 4;

        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers)
        {
            Context = new ActorContext(new LocalActorReference(mailbox)),
        };

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(workers);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_ProcessMessagesCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        const int workers = 4;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert
            foreach (var child in supervisor.Children)
            {
                var response = await child.AskAsync<SetMessage, string>(
                    new SetMessage("hello"),
                    cancellationToken: cancellationToken
                );

                await Assert.That(response).IsEqualTo("HELLO");
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task GetActorReference_Should_ReturnConsistentActorForSameKey(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 4;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act - Get actor reference multiple times with the same key
            var key = "test-key-123";
            var actor1 = supervisor.GetActorReferenceForKey(key);
            var actor2 = supervisor.GetActorReferenceForKey(key);
            var actor3 = supervisor.GetActorReferenceForKey(key);

            // Assert - Same key should always return the same actor
            await Assert.That(actor1).IsSameReferenceAs(actor2);
            await Assert.That(actor2).IsSameReferenceAs(actor3);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task GetActorReference_Should_DistributeAcrossWorkers(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 4;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act - Use many different keys to try to hit different workers
            var actorRefs = Enumerable
                .Range(0, 100)
                .Select(i => supervisor.GetActorReferenceForKey($"key-{i}"))
                .ToList();

            // Assert - Should distribute messages (not all to the same actor)
            var distinctActors = actorRefs.Distinct().Count();
            await Assert.That(distinctActors).IsGreaterThan(1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_RestartWithOneForOneStrategy_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert
            foreach (var child in supervisor.Children)
            {
                var message = Uuid.NewUuid().ToString();
                await child.AskAsync<SetMessage, string>(
                    new SetMessage(message),
                    cancellationToken: cancellationToken
                );

                await Assert.ThrowsAsync<Exception>(async () =>
                    await child.AskAsync<RaiseException, object>(
                        new RaiseException(),
                        cancellationToken: cancellationToken
                    )
                );

                // Wait for restart to complete
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

                var response = await child.AskAsync<GetState, string>(
                    new GetState(),
                    cancellationToken: cancellationToken
                );
                await Assert.That(response).IsEqualTo(string.Empty);
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_RestartWithAllForOneStrategy_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(
            new ActorFactory(),
            workers,
            Strategy.AllForOne
        );

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert - Set state on all children
            foreach (var child in supervisor.Children)
            {
                var message = Uuid.NewUuid().ToString();
                await child.AskAsync<SetMessage, string>(
                    new SetMessage(message),
                    cancellationToken: cancellationToken
                );
            }

            var firstChild = supervisor.Children.First();
            await Assert.ThrowsAsync<Exception>(async () =>
                await firstChild.AskAsync<RaiseException, object>(
                    new RaiseException(),
                    cancellationToken: cancellationToken
                )
            );

            // Wait for restart to complete
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // All children should be restarted (state reset)
            foreach (var child in supervisor.Children)
            {
                var response = await child.AskAsync<GetState, string>(
                    new GetState(),
                    cancellationToken: cancellationToken
                );
                await Assert.That(response).IsEqualTo(string.Empty);
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_Resume_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(
            new ActorFactory(),
            workers,
            failureAction: FailureAction.Resume
        );

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert
            foreach (var child in supervisor.Children)
            {
                var message = Uuid.NewUuid().ToString();
                await child.AskAsync<SetMessage, string>(
                    new SetMessage(message),
                    cancellationToken: cancellationToken
                );

                await Assert.ThrowsAsync<Exception>(async () =>
                    await child.AskAsync<RaiseException, object>(
                        new RaiseException(),
                        cancellationToken: cancellationToken
                    )
                );

                // Wait for resume to complete
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

                var response = await child.AskAsync<GetState, string>(
                    new GetState(),
                    cancellationToken: cancellationToken
                );
                await Assert.That(response).IsEqualTo(message.ToUpper());
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_Stop_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(
            new ActorFactory(),
            workers,
            failureAction: FailureAction.Stop
        );

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert
            foreach (var child in supervisor.Children)
            {
                var message = Uuid.NewUuid().ToString();
                await child.AskAsync<SetMessage, string>(
                    new SetMessage(message),
                    cancellationToken: cancellationToken
                );

                await Assert.ThrowsAsync<Exception>(async () =>
                    await child.AskAsync<RaiseException, object>(
                        new RaiseException(),
                        cancellationToken: cancellationToken
                    )
                );

                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await child.AskAsync<GetState, string>(
                        new GetState(),
                        cancellationToken: cts.Token
                    );
                });
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task CreateActor_Should_ThrowException_WhenCalledAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<SupervisorAlreadyInitializedException>(() =>
            {
                supervisor.CreateActorExposed(new ChannelMailbox());
                return Task.CompletedTask;
            });
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task CreateActor_Should_ThrowException_WhenExceedingMaxWorkers(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new TestablePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            // Act & Assert - supervisor will try to create more than workers limit
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await supervisor.InitializeAsync(cancellationToken)
            );
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task DisposeAsync_Should_CleanupAllActors(CancellationToken cancellationToken)
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        await supervisor.InitializeAsync(cancellationToken);

        // Ensure all children are initialized
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        var childrenBefore = supervisor.Children.ToList();
        await Assert.That(childrenBefore).Count().IsEqualTo(workers);

        // Act
        await supervisor.DisposeAsync();

        // Assert
        await Assert.That(supervisor.Children).Count().IsEqualTo(0);

        await process.StopAsync();
    }

    [Test]
    public async Task BeforeRestartAsync_Should_CleanupAllActors(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        const int workers = 2;
        var supervisor = new SimplePartitionSupervisor(new ActorFactory(), workers);

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        await supervisor.InitializeAsync(cancellationToken);

        // Ensure all children are initialized
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        var childrenBefore = supervisor.Children.ToList();
        await Assert.That(childrenBefore).Count().IsEqualTo(workers);

        // Act
        await supervisor.BeforeRestartAsync(cancellationToken);

        // Assert
        await Assert.That(supervisor.Children).Count().IsEqualTo(0);

        await process.StopAsync();
    }

    [Test]
    public async Task DefaultWorkerCount_Should_UseProcessorCount(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new DefaultWorkerPartitionSupervisor(new ActorFactory());

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(Environment.ProcessorCount);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    public class SimplePartitionSupervisor(
        IActorFactory actorFactory,
        int workers,
        Strategy strategy = Strategy.OneForOne,
        FailureAction failureAction = FailureAction.Restart
    )
        : PartitionSupervisor<SimpleActor>(
            actorFactory,
            new NullLogger<SimplePartitionSupervisor>(),
            workers
        )
    {
        protected override Strategy Strategy => strategy;

        public IActorReference GetActorReferenceForKey(object key) => GetActorReference(key);

        public ActorMetadata CreateActorExposed(IMailbox mailbox) => CreateActor(mailbox);

        protected override FailureAction GetFailureAction(
            ActorMetadata metadata,
            Exception exception
        )
        {
            return failureAction;
        }
    }

    public class TestablePartitionSupervisor(IActorFactory actorFactory, int workers)
        : PartitionSupervisor<SimpleActor>(
            actorFactory,
            new NullLogger<TestablePartitionSupervisor>(),
            workers
        )
    {
        protected override ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            // Try to create more actors than workers limit
            CreateActor(CreateMailbox());
            return ValueTask.CompletedTask;
        }
    }

    public class DefaultWorkerPartitionSupervisor(IActorFactory actorFactory)
        : PartitionSupervisor<SimpleActor>(
            actorFactory,
            new NullLogger<DefaultWorkerPartitionSupervisor>()
        ) { }

    public class SimpleActor : Actor
    {
        private string _state = string.Empty;

        public override ValueTask HandleAsync(
            object? message,
            CancellationToken cancellationToken = default
        )
        {
            if (message is SetMessage simpleMessage)
            {
                _state = simpleMessage.Content.ToUpper();
                Context.Response = _state;
            }
            else if (message is GetState)
            {
                Context.Response = _state;
            }
            else if (message is RaiseException)
            {
                throw new Exception();
            }

            return ValueTask.CompletedTask;
        }
    }

    public record SetMessage(string Content);

    public record GetState();

    public record RaiseException();

    public class ActorFactory : IActorFactory
    {
        public IActor CreateActor(Type actorType)
        {
            return (IActor)Activator.CreateInstance(actorType)!;
        }
    }
}
