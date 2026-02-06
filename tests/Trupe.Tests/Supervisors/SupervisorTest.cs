using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Trupe.ActorReferences;
using Trupe.Factories;
using Trupe.Mailboxes;
using Trupe.Supervisors;

namespace Trupe.Tests.Supervisors;

public class SupervisorTest
{
    [Test]
    public async Task ChildrenCount_Should_BeCorrectlyAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert
                .That(supervisor.Children)
                .Count()
                .IsEqualTo(RuntimeFeature.IsDynamicCodeSupported ? 4 : 2);
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
        var supervisor = new SimpleSupervisor(new ActorFactory());

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
    public async Task Children_Should_RestartWithOneForOneStretagy_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new SimpleSupervisor(new ActorFactory());

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
    public async Task Children_Should_RestartWithAllForOneStretagy_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new SimpleSupervisor(new ActorFactory(), Strategy.AllForOne);

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
            }

            var firstChild = supervisor.Children.First();
            await Assert.ThrowsAsync<Exception>(async () =>
                await firstChild.AskAsync<RaiseException, object>(
                    new RaiseException(),
                    cancellationToken: cancellationToken
                )
            );

            foreach (var child in supervisor.Children.Skip(1))
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
    public async Task Children_Should_Resume_When_ActorThrowExceptionAnd(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new SimpleSupervisor(
            new ActorFactory(),
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
        var supervisor = new SimpleSupervisor(
            new ActorFactory(),
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
    public async Task Children_Should_Escalate_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new SupervisorWithInnerSupervisor(new ActorFactory());

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var innerSupervisor = supervisor.InnerSupervisor;

            // Act & Assert
            var firstChild = innerSupervisor.Children.First();

            var message = Uuid.NewUuid().ToString();
            await firstChild.AskAsync<SetMessage, string>(
                new SetMessage(message),
                cancellationToken: cancellationToken
            );

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await firstChild.AskAsync<RaiseException, object>(
                    new RaiseException(),
                    cancellationToken: cancellationToken
                )
            );

            innerSupervisor = supervisor.InnerSupervisor;
            await Assert.That(firstChild).IsNotSameReferenceAs(innerSupervisor.Children.First());
        }
        finally
        {
            await process.StopAsync();
        }
    }

    public class SimpleSupervisor(
        IActorFactory actorFactory,
        Strategy strategy = Strategy.OneForOne,
        FailureAction failureAction = FailureAction.Restart
    ) : Supervisor(actorFactory, new NullLogger<SimpleSupervisor>())
    {
        protected override Strategy Strategy => strategy;

        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                await AddChildAsync<TypedActor>(cancellationToken);
                await AddChildAsync(typeof(TypedActor), cancellationToken);
            }

            await AddChildAsync<SimpleUntypedActor>(cancellationToken);
            await AddChildAsync(typeof(SimpleUntypedActor), cancellationToken);
        }

        protected override FailureAction GetFailureAction(
            ActorMetadata metadata,
            Exception exception
        )
        {
            return failureAction;
        }
    }

    public class SupervisorWithInnerSupervisor(IActorFactory actorFactory)
        : Supervisor(actorFactory, new NullLogger<SupervisorWithInnerSupervisor>())
    {
        public ISupervisor InnerSupervisor => (SimpleSupervisor)Actors.First().Actor;

        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            await AddChildAsync<SimpleSupervisor>(cancellationToken);
        }
    }

    public class TypedActor
        : Actor,
            IHandleActorMessage<SetMessage>,
            IHandleActorMessage<GetState>,
            IHandleActorMessage<RaiseException>
    {
        private string _state = string.Empty;

        public ValueTask HandleAsync(
            SetMessage message,
            CancellationToken cancellationToken = default
        )
        {
            _state = message.Content.ToUpper();
            Context.Response = _state;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(
            GetState message,
            CancellationToken cancellationToken = default
        )
        {
            Context.Response = _state;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(
            RaiseException message,
            CancellationToken cancellationToken = default
        )
        {
            throw new Exception();
        }
    }

    public class SimpleUntypedActor : Actor
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
            if (actorType == typeof(SimpleSupervisor))
            {
                return new SimpleSupervisor(this, failureAction: FailureAction.Escalate);
            }

            return (IActor)Activator.CreateInstance(actorType)!;
        }
    }
}
