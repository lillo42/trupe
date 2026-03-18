using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Events;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Factories;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Supervisors;
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Messages;
using Trupe.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Tests.Supervisors;

public class SupervisorTest
{
    #region Test Helpers

    private class TestSupervisor(
        IActorFactory actorFactory,
        ILogger logger,
        Strategy? strategy = null,
        int? maxRestarts = null,
        TimeSpan? restartWindow = null,
        Func<CancellationToken, ValueTask>? onInitialize = null
    ) : Supervisor(actorFactory, logger)
    {
        private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;

        private readonly Strategy _strategy = strategy ?? Strategy.OneForOne;
        private readonly int _maxRestarts = maxRestarts ?? 3;
        private readonly TimeSpan _restartWindow = restartWindow ?? TimeSpan.FromSeconds(5);

        protected override Strategy Strategy => _strategy;
        protected override int MaxRestarts => _maxRestarts;
        protected override TimeSpan RestartWindow => _restartWindow;

        protected override ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (_onInitialize != null)
            {
                return _onInitialize(cancellationToken);
            }

            return ValueTask.CompletedTask;
        }

        // Expose protected members for testing
        public new ImmutableList<Child> Children
        {
            get => base.Children;
            set => base.Children = value;
        }

        public new IActorReference AddChild(IChildSpecification specification) =>
            base.AddChild(specification);

        public new IActorReference AddChild(Type actorType) => base.AddChild(actorType);

        public new ValueTask<IActorReference> AddChildAsync(
            IChildSpecification specification,
            CancellationToken cancellationToken = default
        ) => base.AddChildAsync(specification, cancellationToken);

        public new ValueTask<IActorReference> AddChildAsync(
            Type actorType,
            CancellationToken cancellationToken = default
        ) => base.AddChildAsync(actorType, cancellationToken);

        public new void ResetCounter(Child child) => base.ResetCounter(child);

        public new FailureAction GetFailureAction(Child child, Exception exception) =>
            base.GetFailureAction(child, exception);

        public new Task ApplyStopAsync(Child child) => base.ApplyStopAsync(child);

        public new Task ApplyResumeAsync(Child child) => base.ApplyResumeAsync(child);

        public new Task ApplyEscalateAsync(Child child, IMessage message, Exception exception) =>
            base.ApplyEscalateAsync(child, message, exception);

        public new Task ApplyRestartAsync(Child child) => base.ApplyRestartAsync(child);

        public new Child CreateActor(
            IChildSpecification specification,
            LocalActorReference reference
        ) => base.CreateActor(specification, reference);

        public new ValueTask DisposeObjectAsync(object obj) => base.DisposeObjectAsync(obj);

        public new ValueTask ResetMailboxAsync(Child child) => base.ResetMailboxAsync(child);

        public new void HandleFailure(object? sender, ActorFailureEventArgs args) =>
            base.HandleFailure(sender, args);

        public new void HandleTermination(object? sender, ActorTerminateEventArgs args) =>
            base.HandleTermination(sender, args);
    }

    private class SimpleActor : Actor
    {
        public bool Initialized { get; set; }
        public bool BeforeRestartCalled { get; set; }

        public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return ValueTask.CompletedTask;
        }

        public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
        {
            BeforeRestartCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private class SimpleSupervisorActor : Actor, ISupervisor
    {
        public System.Collections.Generic.IEnumerable<IActorReference> Children =>
            Enumerable.Empty<IActorReference>();
    }

    private class DisposableActor : Actor, IAsyncDisposable
    {
        public bool AsyncDisposed { get; set; }

        public ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private class SyncDisposableActor : Actor, IDisposable
    {
        public bool SyncDisposed { get; set; }

        public void Dispose()
        {
            SyncDisposed = true;
        }
    }

    private static TestSupervisor CreateSupervisor(
        IActorFactory? factory = null,
        Strategy? strategy = null,
        int? maxRestarts = null,
        TimeSpan? restartWindow = null,
        Func<CancellationToken, ValueTask>? onInitialize = null
    )
    {
        factory ??= Substitute.For<IActorFactory>();
        var logger = Substitute.For<ILogger>();
        var supervisor = new TestSupervisor(
            factory,
            logger,
            strategy,
            maxRestarts,
            restartWindow,
            onInitialize
        );

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox), new ServiceCollection().BuildServiceProvider().CreateScope());

        return supervisor;
    }

    private static Child CreateChild(
        IActor? actor = null,
        IMailbox? mailbox = null,
        RestartPolicy restartPolicy = RestartPolicy.Permanent
    )
    {
        actor ??= new SimpleActor();
        mailbox ??= new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        actor.Context = new ActorContext(reference, new ServiceCollection().BuildServiceProvider().CreateScope());
        var process = new ActorProcess(actor, mailbox);
        return new Child(actor, mailbox, process, reference, restartPolicy, typeof(SimpleActor));
    }

    private static IActorFactory CreateFactory(IActor actorToReturn)
    {
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(actorToReturn);
        return factory;
    }

    #endregion

    #region Initialization

    [Test]
    public async Task InitializeAsync_Should_CallOnInitializeAsync()
    {
        // Arrange
        var called = false;
        var supervisor = CreateSupervisor(onInitialize: _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        // Act
        await supervisor.InitializeAsync();

        // Assert
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_Should_MarkAsInitialized_PreventingAddChild()
    {
        // Arrange
        var supervisor = CreateSupervisor();

        // Act
        await supervisor.InitializeAsync();

        // Assert
        var action = () => supervisor.AddChild(new ChildSpecification(typeof(SimpleActor)));
        await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
    }

    #endregion

    #region Message Routing

    [Test]
    public async Task HandleAsync_Should_RouteAddActorMessage()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);
        var spec = new ChildSpecification(typeof(SimpleActor));
        var reference = new LocalActorReference(new ChannelMailbox());
        var message = new AddActor(spec, reference);

        // Act
        await supervisor.HandleAsync((object)message);

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task HandleAsync_Should_RouteActorFailedMessage()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);

        var spec = new ChildSpecification(typeof(SimpleActor));
        var reference = new LocalActorReference(new ChannelMailbox());
        supervisor.CreateActor(spec, reference);

        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);
        var failedMessage = new ActorFailed(
            child.Actor,
            tellMessage,
            new InvalidOperationException("test")
        );

        // Act - should not throw (actor found, restart applied)
        await supervisor.HandleAsync((object)failedMessage);

        // Assert - child was restarted so factory called to recreate
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleAsync_Should_RouteActorTerminatedMessage()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);

        var spec = new ChildSpecification(typeof(SimpleActor));
        var reference = new LocalActorReference(new ChannelMailbox());
        supervisor.CreateActor(spec, reference);

        var child = supervisor.Children[0];
        var terminatedMessage = new ActorTerminated(child.Actor, "shutdown");

        // Act - permanent actor should be reset
        await supervisor.HandleAsync((object)terminatedMessage);

        // Assert - factory called to recreate
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleAsync_Should_RouteUnknownMessageToBase()
    {
        // Arrange
        var supervisor = CreateSupervisor();

        // Act & Assert - base HandleAsync throws for unhandled messages
        var action = async () => await supervisor.HandleAsync((object)"unknown message");
        await Assert.That(action).ThrowsException();
    }

    #endregion

    #region Adding Children

    [Test]
    public async Task AddChild_WithType_Should_WorkBeforeInitialization()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);

        // Act
        var actorRef = supervisor.AddChild(typeof(SimpleActor));

        // Assert
        await Assert.That(actorRef).IsNotNull();
    }

    [Test]
    public async Task AddChild_WithSpec_Should_WorkBeforeInitialization()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);
        var spec = new ChildSpecification(typeof(SimpleActor))
        {
            RestartPolicy = RestartPolicy.Transient,
        };

        // Act
        var actorRef = supervisor.AddChild(spec);

        // Assert
        await Assert.That(actorRef).IsNotNull();
    }

    [Test]
    public async Task AddChild_Should_ThrowAfterInitialization()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        await supervisor.InitializeAsync();

        // Act & Assert
        var action = () => supervisor.AddChild(typeof(SimpleActor));
        await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
    }

    [Test]
    public async Task AddChildAsync_WithType_Should_WorkBeforeInitialization()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);

        // Act
        var actorRef = await supervisor.AddChildAsync(typeof(SimpleActor));

        // Assert
        await Assert.That(actorRef).IsNotNull();
    }

    [Test]
    public async Task AddChildAsync_WithSpec_Should_WorkBeforeInitialization()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);
        var spec = new ChildSpecification(typeof(SimpleActor));

        // Act
        var actorRef = await supervisor.AddChildAsync(spec);

        // Assert
        await Assert.That(actorRef).IsNotNull();
    }

    [Test]
    public async Task AddChildAsync_Should_ThrowAfterInitialization()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        await supervisor.InitializeAsync();

        // Act & Assert
        var action = async () => await supervisor.AddChildAsync(typeof(SimpleActor));
        await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
    }

    #endregion

    #region Creating Actors

    [Test]
    public async Task CreateActor_Should_CreateViaFactory_SetContext_AddToChildren()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        var spec = new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox };

        // Act
        var child = supervisor.CreateActor(spec, reference);

        // Assert
        await Assert.That(supervisor.Children).Count().IsEqualTo(1);
        await Assert.That(child.Actor).IsSameReferenceAs(childActor);
        await Assert.That(childActor.Context).IsNotNull();
        factory.Received(1).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task CreateActor_Should_StartProcess_WithInitializeMessage()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = CreateFactory(childActor);
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        var spec = new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox };

        // Act
        var child = supervisor.CreateActor(spec, reference);
        await Task.Delay(100); // Allow initialization message to be processed

        // Assert
        await Assert.That(child.Process.IsRunning).IsTrue();
        await Assert.That(childActor.Initialized).IsTrue();
    }

    [Test]
    public async Task CreateActor_Should_TrackMultipleChildren()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        // Act
        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        // Assert
        await Assert.That(supervisor.Children).Count().IsEqualTo(2);
    }

    [Test]
    public async Task CreateActor_Should_PropagateRestartPolicy_Permanent()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var spec = new ChildSpecification(typeof(SimpleActor))
        {
            Mailbox = mailbox,
            RestartPolicy = RestartPolicy.Permanent,
        };

        // Act
        supervisor.CreateActor(spec, new LocalActorReference(mailbox));

        // Assert
        await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Permanent);
    }

    [Test]
    public async Task CreateActor_Should_PropagateRestartPolicy_Transient()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var spec = new ChildSpecification(typeof(SimpleActor))
        {
            Mailbox = mailbox,
            RestartPolicy = RestartPolicy.Transient,
        };

        // Act
        supervisor.CreateActor(spec, new LocalActorReference(mailbox));

        // Assert
        await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
    }

    [Test]
    public async Task CreateActor_Should_PropagateRestartPolicy_Temporary()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var spec = new ChildSpecification(typeof(SimpleActor))
        {
            Mailbox = mailbox,
            RestartPolicy = RestartPolicy.Temporary,
        };

        // Act
        supervisor.CreateActor(spec, new LocalActorReference(mailbox));

        // Assert
        await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Temporary);
    }

    [Test]
    public async Task CreateActor_Should_DefaultRestartPolicy_Permanent()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var spec = new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox };

        // Act
        supervisor.CreateActor(spec, new LocalActorReference(mailbox));

        // Assert - ChildSpecification defaults to Permanent
        await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Permanent);
    }

    #endregion

    #region Actor Failure Handling

    [Test]
    public async Task HandleActorFailed_PermanentActor_OneForOne_Should_RestartActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);

        // Act
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, tellMessage, new Exception("fail"))
        );

        // Assert - factory called twice: initial + restart
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorFailed_PermanentActor_AllForOne_Should_RestartAllActors()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);

        // Act
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, tellMessage, new Exception("fail"))
        );

        // Assert - factory called 4 times: 2 initial + 2 restart
        factory.Received(4).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorFailed_TemporaryActor_Should_StopWithoutRestart()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Temporary,
            },
            reference
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);

        // Act
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, tellMessage, new Exception("fail"))
        );

        // Assert - actor should be stopped, not restarted
        await Assert.That(child.Process.IsRunning).IsFalse();
    }

    [Test]
    public async Task HandleActorFailed_TransientActor_Should_RestartOnFailure()
    {
        // ACountrrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Transient,
            },
            reference
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);

        // Act
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, tellMessage, new Exception("fail"))
        );

        // Assert - transient actors are restarted on failure (factory called twice: initial + restart)
        factory.Received(2).CreateActor(typeof(SimpleActor));
        await Assert.That(child.Process.IsRunning).IsTrue();
    }

    [Test]
    public async Task HandleActorFailed_WithAskMessage_Should_CancelAskMessage()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var askMessage = new LocalAskMessage("test", [], CancellationToken.None);

        // Act
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, askMessage, new Exception("fail"))
        );

        // Assert - ask message should be canceled
        var action = async () => await askMessage.AsTask();
        await Assert.That(action).Throws<TaskCanceledException>();
    }

    [Test]
    public async Task HandleActorFailed_WithEscalateExceptionContainingAskMessage_Should_CancelNestedAskMessage()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var nestedAskMessage = new LocalAskMessage("nested", [], CancellationToken.None);
        var tellMessage = new LocalTellMessage("test", []);

        var escalateException = new EscalateFailureException(
            "escalated",
            reference,
            nestedAskMessage,
            new Exception("inner")
        );

        // Act
        await supervisor.HandleAsync(new ActorFailed(child.Actor, tellMessage, escalateException));

        // Assert
        var action = async () => await nestedAskMessage.AsTask();
        await Assert.That(action).Throws<TaskCanceledException>();
    }

    [Test]
    public async Task HandleActorFailed_UnknownActor_Should_ReturnWithoutAction()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var unknownActor = new SimpleActor();
        var tellMessage = new LocalTellMessage("test", []);

        // Act & Assert - should not throw
        await supervisor.HandleAsync(
            new ActorFailed(unknownActor, tellMessage, new Exception("fail"))
        );
    }

    [Test]
    public async Task HandleActorFailed_ExceedsMaxRestarts_Should_Escalate()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(
            factory: factory,
            maxRestarts: 1,
            restartWindow: TimeSpan.FromMinutes(10)
        );

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);

        // First failure - restart
        await supervisor.HandleAsync(
            new ActorFailed(child.Actor, tellMessage, new Exception("fail1"))
        );

        // Get the new child after restart
        var restartedChild = supervisor.Children[0];

        // Second failure - should escalate
        var action = async () =>
            await supervisor.HandleAsync(
                new ActorFailed(restartedChild.Actor, tellMessage, new Exception("fail2"))
            );

        await Assert.That(action).Throws<EscalateFailureException>();
    }

    #endregion

    #region §6 Actor Termination Handling

    [Test]
    public async Task HandleActorTerminated_PermanentActor_Should_ResetActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync(new ActorTerminated(child.Actor, "done"));

        // Assert - factory called twice: initial + reset
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorTerminated_TransientActor_Should_TerminateReference()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Transient,
            },
            reference
        );
        var child = supervisor.Children[0];
        var terminateCalled = false;
        child.Reference.OnTerminate += (_, _) => terminateCalled = true;

        // Act
        await supervisor.HandleAsync(new ActorTerminated(child.Actor, "done"));

        // Assert - not restarted, terminate event raised
        await Assert.That(terminateCalled).IsTrue();
    }

    [Test]
    public async Task HandleActorTerminated_TemporaryActor_Should_TerminateReference()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Temporary,
            },
            reference
        );
        var child = supervisor.Children[0];
        var terminateCalled = false;
        child.Reference.OnTerminate += (_, _) => terminateCalled = true;

        // Act
        await supervisor.HandleAsync(new ActorTerminated(child.Actor, "done"));

        // Assert
        await Assert.That(terminateCalled).IsTrue();
    }

    [Test]
    public async Task HandleActorTerminated_UnknownActor_Should_ReturnWithoutAction()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var unknownActor = new SimpleActor();

        // Act & Assert - should not throw
        await supervisor.HandleAsync(new ActorTerminated(unknownActor, "done"));
    }

    #endregion

    #region §7 Restart Counter

    [Test]
    public async Task ResetCounter_Should_ResetWhenWindowElapsed()
    {
        // Arrange
        var supervisor = CreateSupervisor(restartWindow: TimeSpan.FromMilliseconds(1));
        var child = CreateChild();
        child.RestartCount = 5;
        child.LastRestartTime = DateTimeOffset.UtcNow.AddSeconds(-10);

        // Act
        supervisor.ResetCounter(child);

        // Assert
        await Assert.That(child.RestartCount).IsEqualTo(0);
    }

    [Test]
    public async Task ResetCounter_Should_PreserveCountWithinWindow()
    {
        // Arrange
        var supervisor = CreateSupervisor(restartWindow: TimeSpan.FromMinutes(10));
        var child = CreateChild();
        child.RestartCount = 2;
        child.LastRestartTime = DateTimeOffset.UtcNow;

        // Act
        supervisor.ResetCounter(child);

        // Assert
        await Assert.That(child.RestartCount).IsEqualTo(2);
    }

    #endregion

    #region §8 Failure Action

    [Test]
    public async Task GetFailureAction_Should_ReturnRestart_WhenBelowMaxRestarts()
    {
        // Arrange
        var supervisor = CreateSupervisor(maxRestarts: 3);
        var child = CreateChild();
        child.RestartCount = 1;

        // Act
        var action = supervisor.GetFailureAction(child, new Exception("fail"));

        // Assert
        await Assert.That(action).IsEqualTo(FailureAction.Restart);
    }

    [Test]
    public async Task GetFailureAction_Should_ReturnEscalate_WhenAtMaxRestarts()
    {
        // Arrange
        var supervisor = CreateSupervisor(maxRestarts: 3);
        var child = CreateChild();
        child.RestartCount = 3;

        // Act
        var action = supervisor.GetFailureAction(child, new Exception("fail"));

        // Assert
        await Assert.That(action).IsEqualTo(FailureAction.Escalate);
    }

    [Test]
    public async Task GetFailureAction_Should_ReturnEscalate_WhenAboveMaxRestarts()
    {
        // Arrange
        var supervisor = CreateSupervisor(maxRestarts: 3);
        var child = CreateChild();
        child.RestartCount = 5;

        // Act
        var action = supervisor.GetFailureAction(child, new Exception("fail"));

        // Assert
        await Assert.That(action).IsEqualTo(FailureAction.Escalate);
    }

    #endregion

    #region §9 Apply Actions

    [Test]
    public async Task ApplyStopAsync_OneForOne_Should_StopOnlyFailedActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.OneForOne);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        var failedChild = supervisor.Children[0];
        var otherChild = supervisor.Children[1];

        // Act
        await supervisor.ApplyStopAsync(failedChild);

        // Assert
        await Assert.That(failedChild.Process.IsRunning).IsFalse();
        await Assert.That(otherChild.Process.IsRunning).IsTrue();
    }

    [Test]
    public async Task ApplyStopAsync_AllForOne_Should_StopAllChildren()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        var failedChild = supervisor.Children[0];

        // Act
        await supervisor.ApplyStopAsync(failedChild);

        // Assert
        await Assert.That(supervisor.Children[0].Process.IsRunning).IsFalse();
        await Assert.That(supervisor.Children[1].Process.IsRunning).IsFalse();
    }

    [Test]
    public async Task ApplyResumeAsync_Should_StopAndRestartProcess()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            new LocalActorReference(mailbox)
        );
        var child = supervisor.Children[0];

        // Act
        await supervisor.ApplyResumeAsync(child);

        // Assert - process should be running again
        await Assert.That(child.Process.IsRunning).IsTrue();
    }

    [Test]
    public async Task ApplyEscalateAsync_Should_ThrowEscalateFailureException()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var tellMessage = new LocalTellMessage("test", []);
        var innerException = new InvalidOperationException("inner");

        // Act & Assert
        var action = async () =>
            await supervisor.ApplyEscalateAsync(child, tellMessage, innerException);
        await Assert.That(action).Throws<EscalateFailureException>();
    }

    [Test]
    public async Task ApplyRestartAsync_OneForOne_Should_ResetOnlyFailedActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.OneForOne);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        var child = supervisor.Children[0];
        var originalActor = child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert - restart count incremented, actor replaced
        await Assert.That(child.RestartCount).IsEqualTo(1);
        await Assert.That(child.Actor).IsNotSameReferenceAs(originalActor);
        // factory: 2 initial + 1 restart
        factory.Received(3).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task ApplyRestartAsync_AllForOne_Should_ResetAllChildren()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        var child = supervisor.Children[0];

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert - factory: 2 initial + 2 restart
        factory.Received(4).CreateActor(typeof(SimpleActor));
        await Assert.That(child.RestartCount).IsEqualTo(1);
    }

    #endregion

    #region §10 Actor Reset

    [Test]
    public async Task ResetActorAsync_Should_RecreateActorViaFactory()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var originalActor = child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert
        await Assert.That(child.Actor).IsNotSameReferenceAs(originalActor);
        await Assert.That(child.Actor.Context).IsNotNull();
        await Assert.That(child.Process.IsRunning).IsTrue();
    }

    [Test]
    public async Task ResetActorAsync_Should_CallBeforeRestartOnOldActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            new LocalActorReference(mailbox)
        );
        var child = supervisor.Children[0];
        var originalActor = (SimpleActor)child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert
        await Assert.That(originalActor.BeforeRestartCalled).IsTrue();
    }

    [Test]
    public async Task ResetMailboxAsync_Should_CleanMailbox_WhenChildIsSupervisor()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var mailbox = Substitute.For<IMailbox>();
        var supervisorActor = new SimpleSupervisorActor();
        supervisorActor.Context = new ActorContext(new LocalActorReference(new ChannelMailbox()), new ServiceCollection().BuildServiceProvider().CreateScope());
        var reference = new LocalActorReference(new ChannelMailbox());
        var process = new ActorProcess(supervisorActor, new ChannelMailbox());
        var child = new Child(
            supervisorActor,
            mailbox,
            process,
            reference,
            RestartPolicy.Permanent,
            typeof(SimpleSupervisorActor)
        );

        // Act
        await supervisor.ResetMailboxAsync(child);

        // Assert
        await mailbox.Received(1).CleanAsync();
    }

    [Test]
    public async Task ResetMailboxAsync_Should_NotCleanMailbox_WhenChildIsNotSupervisor()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var mailbox = Substitute.For<IMailbox>();
        var simpleActor = new SimpleActor();
        simpleActor.Context = new ActorContext(new LocalActorReference(new ChannelMailbox()), new ServiceCollection().BuildServiceProvider().CreateScope());
        var reference = new LocalActorReference(new ChannelMailbox());
        var process = new ActorProcess(simpleActor, new ChannelMailbox());
        var child = new Child(
            simpleActor,
            mailbox,
            process,
            reference,
            RestartPolicy.Permanent,
            typeof(SimpleActor)
        );

        // Act
        await supervisor.ResetMailboxAsync(child);

        // Assert
        await mailbox.DidNotReceive().CleanAsync();
    }

    #endregion

    #region §11 BeforeRestart

    [Test]
    public async Task BeforeRestartActorAsync_Should_CallBeforeRestartOnActor()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            new LocalActorReference(mailbox)
        );
        var child = supervisor.Children[0];
        var actorBeforeRestart = (SimpleActor)child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert
        await Assert.That(actorBeforeRestart.BeforeRestartCalled).IsTrue();
    }

    [Test]
    public async Task BeforeRestartActorAsync_Should_SwallowException()
    {
        // Arrange
        var actor = Substitute.For<IActor>();
        actor
            .BeforeRestartAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("boom")));

        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(actor, new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );

        var child = supervisor.Children[0];

        // Act & Assert - should not throw
        await supervisor.ApplyRestartAsync(child);
    }

    #endregion

    #region §12 Event Handlers

    [Test]
    public async Task HandleFailure_Should_SendActorFailedToSelf()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);
        var supervisorMailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(supervisorMailbox), new ServiceCollection().BuildServiceProvider().CreateScope());

        var actor = new SimpleActor();
        var tellMessage = new LocalTellMessage("test", []);
        var exception = new Exception("fail");
        var args = new ActorFailureEventArgs(actor, tellMessage, exception);

        // Act
        supervisor.HandleFailure(null, args);

        // Assert - message should have been enqueued to supervisor's mailbox
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in supervisorMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<ActorFailed>();
            var failed = (ActorFailed)msg.Payload;
            await Assert.That(failed.Actor).IsSameReferenceAs(actor);
            await Assert.That(failed.Exception).IsSameReferenceAs(exception);
            break;
        }
    }

    [Test]
    public async Task HandleTermination_Should_SendActorTerminatedToSelf()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var supervisorMailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(supervisorMailbox), new ServiceCollection().BuildServiceProvider().CreateScope());

        var actor = new SimpleActor();
        var args = new ActorTerminateEventArgs(actor, "shutdown");

        // Act
        supervisor.HandleTermination(null, args);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in supervisorMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<ActorTerminated>();
            var terminated = (ActorTerminated)msg.Payload;
            await Assert.That(terminated.Actor).IsSameReferenceAs(actor);
            await Assert.That(terminated.Reason).IsEqualTo("shutdown");
            break;
        }
    }

    #endregion

    #region §13 Disposal

    [Test]
    public async Task DisposeAsync_Should_DisposeAllChildrenAndClearList()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor)) { Mailbox = mailbox },
            new LocalActorReference(mailbox)
        );
        var child = supervisor.Children[0];
        var disposableActor = (DisposableActor)child.Actor;

        // Act
        await supervisor.DisposeAsync();

        // Assert
        await Assert.That(disposableActor.AsyncDisposed).IsTrue();
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisposeObjectAsync_Should_CallDisposeAsync_ForAsyncDisposable()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var disposable = new DisposableActor();

        // Act
        await supervisor.DisposeObjectAsync(disposable);

        // Assert
        await Assert.That(disposable.AsyncDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeObjectAsync_Should_CallDispose_ForSyncDisposable()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var disposable = new SyncDisposableActor();

        // Act
        await supervisor.DisposeObjectAsync(disposable);

        // Assert
        await Assert.That(disposable.SyncDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeObjectAsync_Should_NoOp_ForNonDisposable()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var obj = new SimpleActor();

        // Act & Assert - should not throw
        await supervisor.DisposeObjectAsync(obj);
    }

    #endregion

    #region §14 Supervisor BeforeRestart

    [Test]
    public async Task BeforeRestartAsync_Should_StopAndDisposeAllChildren()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
        var supervisor = CreateSupervisor(factory: factory);

        var mailbox1 = new ChannelMailbox();
        var mailbox2 = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor)) { Mailbox = mailbox1 },
            new LocalActorReference(mailbox1)
        );
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor)) { Mailbox = mailbox2 },
            new LocalActorReference(mailbox2)
        );

        var actor1 = (DisposableActor)supervisor.Children[0].Actor;
        var actor2 = (DisposableActor)supervisor.Children[1].Actor;

        // Act
        await supervisor.BeforeRestartAsync();

        // Assert
        await Assert.That(actor1.AsyncDisposed).IsTrue();
        await Assert.That(actor2.AsyncDisposed).IsTrue();
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
    }

    #endregion
}
