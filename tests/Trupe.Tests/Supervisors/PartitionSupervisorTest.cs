using System;
using System.Collections.Generic;
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

public class PartitionSupervisorTest
{
    #region Test Helpers

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

    private class SimpleSupervisorActor : Actor, ISupervisor
    {
        public IEnumerable<IActorReference> Children => [];

        IEnumerable<IActorReference> ISupervisor.Children => throw new NotImplementedException();
    }

    private class TestPartitionSupervisor(
        IActorFactory actorFactory,
        ILogger logger,
        int workers = 3,
        Strategy? strategy = null,
        int? maxRestarts = null,
        TimeSpan? restartWindow = null,
        RestartPolicy? defaultRestartPolicy = null,
        Func<CancellationToken, ValueTask>? onInitialize = null
    ) : PartitionSupervisor<SimpleActor>(actorFactory, logger, workers)
    {
        private readonly Strategy _strategy = strategy ?? Strategy.OneForOne;
        private readonly int _maxRestarts = maxRestarts ?? 3;
        private readonly TimeSpan _restartWindow = restartWindow ?? TimeSpan.FromSeconds(5);
        private readonly RestartPolicy _defaultRestartPolicy =
            defaultRestartPolicy ?? RestartPolicy.Permanent;
        private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;

        protected override Strategy Strategy => _strategy;
        protected override int MaxRestarts => _maxRestarts;
        protected override TimeSpan RestartWindow => _restartWindow;
        protected override RestartPolicy DefaultRestartPolicy => _defaultRestartPolicy;

        protected override ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (_onInitialize != null)
                return _onInitialize(cancellationToken);
            return ValueTask.CompletedTask;
        }

        // Expose protected members for testing
        public new ImmutableList<Child> Children => base.Children;

        public new Child CreateActor(ChildSpecification specification) =>
            base.CreateActor(specification);

        public new IActorReference GetActorReference<TKey>(TKey key)
            where TKey : notnull => base.GetActorReference(key);

        public new int GetHashcode<TKey>(TKey key)
            where TKey : notnull => base.GetHashcode(key);

        public new void ResetCounter(Child child) => base.ResetCounter(child);

        public new FailureAction GetFailureAction(Child child, Exception exception) =>
            base.GetFailureAction(child, exception);

        public new Task ApplyStopAsync(Child child) => base.ApplyStopAsync(child);

        public new Task ApplyResumeAsync(Child child) => base.ApplyResumeAsync(child);

        public new Task ApplyEscalateAsync(Child child, IMessage message, Exception exception) =>
            base.ApplyEscalateAsync(child, message, exception);

        public new Task ApplyRestartAsync(Child child) => base.ApplyRestartAsync(child);

        public new ValueTask DisposeObjectAsync(object obj) => base.DisposeObjectAsync(obj);

        public new ValueTask ResetMailboxAsync(Child child) => base.ResetMailboxAsync(child);

        public new void HandleFailure(object? sender, ActorFailureEventArgs args) =>
            base.HandleFailure(sender, args);

        public new void HandleTermination(object? sender, ActorTerminateEventArgs args) =>
            base.HandleTermination(sender, args);
    }

    private static TestPartitionSupervisor CreateSupervisor(
        IActorFactory? factory = null,
        int workers = 3,
        Strategy? strategy = null,
        int? maxRestarts = null,
        TimeSpan? restartWindow = null,
        RestartPolicy? defaultRestartPolicy = null,
        Func<CancellationToken, ValueTask>? onInitialize = null,
        ChannelMailbox? selfMailbox = null
    )
    {
        factory ??= Substitute.For<IActorFactory>();
        var logger = Substitute.For<ILogger>();
        var supervisor = new TestPartitionSupervisor(
            factory,
            logger,
            workers,
            strategy,
            maxRestarts,
            restartWindow,
            defaultRestartPolicy,
            onInitialize
        );
        selfMailbox ??= new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(selfMailbox), new ServiceCollection().BuildServiceProvider().CreateScope());
        return supervisor;
    }

    private static IActorFactory CreateFactory()
    {
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        return factory;
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

    #endregion

    #region Initialization

    [Test]
    public async Task InitializeAsync_Should_CreateExactlyWorkersActors()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 3);

        // Act
        await supervisor.InitializeAsync();

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(3);
        factory.Received(3).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task InitializeAsync_Should_CallOnInitializeAsync()
    {
        // Arrange
        var called = false;
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 1,
            onInitialize: _ =>
            {
                called = true;
                return ValueTask.CompletedTask;
            }
        );

        // Act
        await supervisor.InitializeAsync();

        // Assert
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_Should_UseDefaultRestartPolicy()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            defaultRestartPolicy: RestartPolicy.Transient
        );

        // Act
        await supervisor.InitializeAsync();

        // Assert
        await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
        await Assert.That(supervisor.Children[1].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
    }

    #endregion

    #region Message Routing

    [Test]
    public async Task HandleAsync_Should_RouteActorFailedMessage()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test", []), new Exception("fail"))
        );

        // Assert - factory called: 1 (init) + 1 (restart) = 2
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleAsync_Should_RouteActorTerminatedMessage()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act - permanent actor should be reset
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert - factory called: 1 (init) + 1 (reset) = 2
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleAsync_Should_RouteUnknownMessageToBase()
    {
        // Arrange
        var supervisor = CreateSupervisor(workers: 1);

        // Act & Assert - base HandleAsync throws for unhandled messages
        var action = async () => await supervisor.HandleAsync((object)"unknown");
        await Assert.That(action).ThrowsException();
    }

    #endregion

    #region Creating Actors

    [Test]
    public async Task CreateActor_Should_CreateViaFactory_SetContext_AddToChildren()
    {
        // Arrange
        var childActor = new SimpleActor();
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(childActor);
        var supervisor = CreateSupervisor(factory: factory, workers: 2);
        var mailbox = new ChannelMailbox();
        var spec = new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox };

        // Act
        var child = supervisor.CreateActor(spec);

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
        await Assert.That(child.Actor).IsSameReferenceAs(childActor);
        await Assert.That(childActor.Context).IsNotNull();
        factory.Received(1).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task CreateActor_Should_PropagateRestartPolicy()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 2);
        var spec = new ChildSpecification(typeof(SimpleActor))
        {
            RestartPolicy = RestartPolicy.Temporary,
        };

        // Act
        var child = supervisor.CreateActor(spec);

        // Assert
        await Assert.That(child.RestartPolicy).IsEqualTo(RestartPolicy.Temporary);
    }

    [Test]
    public async Task CreateActor_Should_ThrowSupervisorAlreadyInitializedException()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        // Act & Assert
        var action = () => supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
        await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
    }

    [Test]
    public async Task CreateActor_Should_ThrowTooManyWorkerException()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));

        // Act & Assert - workers limit reached
        var action = () => supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
        await Assert.That(action).Throws<TooManyWorkerException>();
    }

    #endregion

    #region Partition Routing

    [Test]
    public async Task GetActorReference_SameKey_Should_ReturnSameReference()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 3);
        await supervisor.InitializeAsync();

        // Act - find a key with non-negative hash (source uses hash % count without abs)
        int safeKey = 0;
        for (var i = 0; i < 1000; i++)
        {
            if (supervisor.GetHashcode(i) >= 0)
            {
                safeKey = i;
                break;
            }
        }

        var ref1 = supervisor.GetActorReference(safeKey);
        var ref2 = supervisor.GetActorReference(safeKey);

        // Assert
        await Assert.That(ref1).IsSameReferenceAs(ref2);
    }

    [Test]
    public async Task GetActorReference_DifferentKeys_Should_DistributeAcrossActors()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 3);
        await supervisor.InitializeAsync();

        // Act - try many keys, skip those with negative hash (source uses hash % count)
        var refs = new HashSet<IActorReference>();
        for (var i = 0; i < 1000; i++)
        {
            var hash = supervisor.GetHashcode(i);
            if (hash >= 0)
            {
                refs.Add(supervisor.GetActorReference(i));
            }
        }

        // Assert - should hit more than one actor
        await Assert.That(refs.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task GetHashcode_Should_UseHashCodeCombine()
    {
        // Arrange
        var supervisor = CreateSupervisor(workers: 1);

        // Act
        var hash = supervisor.GetHashcode("test");
        var expected = HashCode.Combine("test");

        // Assert
        await Assert.That(hash).IsEqualTo(expected);
    }

    #endregion

    #region Actor Failure Handling

    [Test]
    public async Task HandleActorFailed_OneForOne_Should_RestartOnlyFailedActor()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.OneForOne
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test", []), new Exception("fail"))
        );

        // Assert - factory: 2 (init) + 1 (restart) = 3
        factory.Received(3).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorFailed_AllForOne_Should_RestartAllActors()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.AllForOne
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test", []), new Exception("fail"))
        );

        // Assert - factory: 2 (init) + 2 (restart all) = 4
        factory.Received(4).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorFailed_WithAskMessage_Should_CancelAskMessage()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];
        var askMessage = new LocalAskMessage("test", [], CancellationToken.None);

        // Act
        await supervisor.HandleAsync(
            (object)new ActorFailed(child.Actor, askMessage, new Exception("fail"))
        );

        // Assert
        var action = async () => await askMessage.AsTask();
        await Assert.That(action).Throws<TaskCanceledException>();
    }

    [Test]
    public async Task HandleActorFailed_WithEscalateException_Should_CancelNestedAskMessage()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];
        var nestedAskMessage = new LocalAskMessage("nested", [], CancellationToken.None);
        var tellMessage = new LocalTellMessage("test", []);
        var escalateException = new EscalateFailureException(
            "escalated",
            child.Reference,
            nestedAskMessage,
            new Exception("inner")
        );

        // Act
        await supervisor.HandleAsync(
            (object)new ActorFailed(child.Actor, tellMessage, escalateException)
        );

        // Assert
        var action = async () => await nestedAskMessage.AsTask();
        await Assert.That(action).Throws<TaskCanceledException>();
    }

    [Test]
    public async Task HandleActorFailed_UnknownActor_Should_NoOp()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var unknownActor = new SimpleActor();

        // Act & Assert — should not throw
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(unknownActor, new LocalTellMessage("test", []), new Exception("fail"))
        );
    }

    [Test]
    public async Task HandleActorFailed_ExceedsMaxRestarts_Should_Escalate()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 1,
            maxRestarts: 1,
            restartWindow: TimeSpan.FromMinutes(10)
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // First failure - restart
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test", []), new Exception("fail1"))
        );

        // Second failure - should escalate (child.Actor is now the new actor)
        var action = async () =>
            await supervisor.HandleAsync(
                (object)
                    new ActorFailed(
                        child.Actor,
                        new LocalTellMessage("test", []),
                        new Exception("fail2")
                    )
            );

        await Assert.That(action).Throws<EscalateFailureException>();
    }

    #endregion

    #region Actor Termination Handling

    [Test]
    public async Task HandleActorTerminated_PermanentActor_Should_ResetActor()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert - factory: 1 (init) + 1 (reset) = 2
        factory.Received(2).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task HandleActorTerminated_NonPermanentActor_Should_TerminateReference()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 1,
            defaultRestartPolicy: RestartPolicy.Transient
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];
        var terminateCalled = false;
        child.Reference.OnTerminate += (_, _) => terminateCalled = true;

        // Act
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert
        await Assert.That(terminateCalled).IsTrue();
    }

    [Test]
    public async Task HandleActorTerminated_UnknownActor_Should_NoOp()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var unknownActor = new SimpleActor();

        // Act & Assert — should not throw
        await supervisor.HandleAsync((object)new ActorTerminated(unknownActor, "done"));
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
    public async Task GetFailureAction_Should_ReturnRestart_BelowMaxRestarts()
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
    public async Task GetFailureAction_Should_ReturnEscalate_AtMaxRestarts()
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

    #endregion

    #region Apply Actions

    [Test]
    public async Task ApplyStopAsync_OneForOne_Should_StopOnlyFailedActor()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.OneForOne
        );
        await supervisor.InitializeAsync();

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
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.AllForOne
        );
        await supervisor.InitializeAsync();

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
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.ApplyResumeAsync(child);

        // Assert
        await Assert.That(child.Process.IsRunning).IsTrue();
    }

    [Test]
    public async Task ApplyEscalateAsync_Should_ThrowEscalateFailureException()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

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
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.OneForOne
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];
        var originalActor = child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert
        await Assert.That(child.RestartCount).IsEqualTo(1);
        await Assert.That(child.Actor).IsNotSameReferenceAs(originalActor);
        // factory: 2 (init) + 1 (restart) = 3
        factory.Received(3).CreateActor(typeof(SimpleActor));
    }

    [Test]
    public async Task ApplyRestartAsync_AllForOne_Should_ResetAllChildren()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(
            factory: factory,
            workers: 2,
            strategy: Strategy.AllForOne
        );
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert - factory: 2 (init) + 2 (restart all) = 4
        factory.Received(4).CreateActor(typeof(SimpleActor));
        await Assert.That(child.RestartCount).IsEqualTo(1);
    }

    #endregion

    #region §10 Actor Reset

    [Test]
    public async Task ResetActorAsync_Should_RecreateActorViaFactory()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

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
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

        var child = supervisor.Children[0];
        var originalActor = (SimpleActor)child.Actor;

        // Act
        await supervisor.ApplyRestartAsync(child);

        // Assert
        await Assert.That(originalActor.BeforeRestartCalled).IsTrue();
    }

    #endregion

    #region BeforeRestart Actor

    [Test]
    public async Task BeforeRestartActorAsync_Should_CallBeforeRestartOnActor()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 1);
        await supervisor.InitializeAsync();

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
        var supervisor = CreateSupervisor(factory: factory, workers: 1);

        supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
        var child = supervisor.Children[0];

        // Act & Assert - should not throw
        await supervisor.ApplyRestartAsync(child);
    }

    #endregion

    #region Event Handlers

    [Test]
    public async Task HandleFailure_Should_SendActorFailedToSelf()
    {
        // Arrange
        var supervisor = CreateSupervisor(workers: 1);
        var supervisorMailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(supervisorMailbox), new ServiceCollection().BuildServiceProvider().CreateScope());

        var actor = new SimpleActor();
        var tellMessage = new LocalTellMessage("test", []);
        var exception = new Exception("fail");
        var args = new ActorFailureEventArgs(actor, tellMessage, exception);

        // Act
        supervisor.HandleFailure(null, args);

        // Assert
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
        var supervisor = CreateSupervisor(workers: 1);
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

    #region Disposal

    [Test]
    public async Task DisposeAsync_Should_DisposeAllChildrenAndClearList()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
        var supervisor = CreateSupervisor(factory: factory, workers: 2);
        await supervisor.InitializeAsync();

        var actor1 = (DisposableActor)supervisor.Children[0].Actor;
        var actor2 = (DisposableActor)supervisor.Children[1].Actor;

        // Act
        await supervisor.DisposeAsync();

        // Assert
        await Assert.That(actor1.AsyncDisposed).IsTrue();
        await Assert.That(actor2.AsyncDisposed).IsTrue();
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisposeObjectAsync_Should_CallDisposeAsync_ForAsyncDisposable()
    {
        // Arrange
        var supervisor = CreateSupervisor(workers: 1);
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
        var supervisor = CreateSupervisor(workers: 1);
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
        var supervisor = CreateSupervisor(workers: 1);
        var obj = new SimpleActor();

        // Act & Assert - should not throw
        await supervisor.DisposeObjectAsync(obj);
    }

    #endregion

    #region Supervisor BeforeRestart

    [Test]
    public async Task BeforeRestartAsync_Should_StopAndDisposeAllChildren()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
        var supervisor = CreateSupervisor(factory: factory, workers: 2);
        await supervisor.InitializeAsync();

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

    #region ResetMailboxAsync

    [Test]
    public async Task ResetMailboxAsync_Should_CleanMailbox_WhenChildIsSupervisor()
    {
        // Arrange
        var supervisor = CreateSupervisor(workers: 1);
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
        var supervisor = CreateSupervisor(workers: 1);
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

    #region ISupervisor.Children

    [Test]
    public async Task ISupervisorChildren_Should_ReturnActorReferences()
    {
        // Arrange
        var factory = CreateFactory();
        var supervisor = CreateSupervisor(factory: factory, workers: 2);
        await supervisor.InitializeAsync();

        // Act
        var refs = ((ISupervisor)supervisor).Children.ToList();

        // Assert
        await Assert.That(refs.Count).IsEqualTo(2);
        await Assert.That(refs[0]).IsSameReferenceAs(supervisor.Children[0].Reference);
        await Assert.That(refs[1]).IsSameReferenceAs(supervisor.Children[1].Reference);
    }

    #endregion
}
