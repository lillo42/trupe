using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Factories;
using Trupe.Abstractions.Supervisors;
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Messages;
using Trupe.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Tests.Supervisors;

public class DynamicSupervisorTest
{
    #region Test Helpers

    private class TestDynamicSupervisor(
        IActorFactory actorFactory,
        ILogger logger,
        Func<CancellationToken, ValueTask>? onInitialize = null
    ) : DynamicSupervisor(actorFactory, logger)
    {
        private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;

        protected override ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (_onInitialize != null)
                return _onInitialize(cancellationToken);
            return ValueTask.CompletedTask;
        }

        public Strategy TestableStrategy => Strategy;

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

        public new Child CreateActor(
            IChildSpecification specification,
            LocalActorReference reference
        ) => base.CreateActor(specification, reference);

        public new void RemoveActor(IActorReference reference) => base.RemoveActor(reference);

        public new ValueTask RemoveActorAsync(
            IActorReference reference,
            CancellationToken cancellationToken = default
        ) => base.RemoveActorAsync(reference, cancellationToken);
    }

    private class SimpleActor : Actor
    {
        public bool Initialized { get; set; }

        public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
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

    private static TestDynamicSupervisor CreateSupervisor(
        IActorFactory? factory = null,
        ChannelMailbox? selfMailbox = null,
        Func<CancellationToken, ValueTask>? onInitialize = null
    )
    {
        factory ??= Substitute.For<IActorFactory>();
        var logger = Substitute.For<ILogger>();
        var supervisor = new TestDynamicSupervisor(factory, logger, onInitialize);
        selfMailbox ??= new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(selfMailbox));
        return supervisor;
    }

    private static IActorFactory CreateFactory<TActor>()
        where TActor : Actor, new()
    {
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new TActor());
        return factory;
    }

    #endregion

    #region Strategy

    [Test]
    public async Task Strategy_Should_AlwaysBeOneForOne()
    {
        // Arrange
        var supervisor = CreateSupervisor();

        // Assert
        await Assert.That(supervisor.TestableStrategy).IsEqualTo(Strategy.OneForOne);
    }

    #endregion

    #region HandleAsync(RemoveChild)

    [Test]
    public async Task HandleRemoveChild_Should_RemoveFromChildren_StopDispose_NullRefs()
    {
        // Arrange
        var factory = CreateFactory<DisposableActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var actor = (DisposableActor)child.Actor;
        var actorToRemove = child.Actor;

        // Act
        await supervisor.HandleAsync(new RemoveChild(actorToRemove));

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
        await Assert.That(actor.AsyncDisposed).IsTrue();
        await Assert.That(child.Actor).IsNull();
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task HandleRemoveChild_Should_StopRunningProcess()
    {
        // Arrange
        var factory = CreateFactory<SimpleActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        await Task.Delay(50);
        await Assert.That(child.Process.IsRunning).IsTrue();

        var actorToRemove = child.Actor;

        // Act
        await supervisor.HandleAsync(new RemoveChild(actorToRemove));

        // Assert — process stopped and nulled
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task HandleRemoveChild_UnknownActor_Should_NoOp()
    {
        // Arrange
        var factory = CreateFactory<SimpleActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            new LocalActorReference(mailbox)
        );

        // Act — remove an actor that's not a child
        await supervisor.HandleAsync(new RemoveChild(new SimpleActor()));

        // Assert — children unchanged
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task HandleRemoveChild_NullActor_Should_NoOp()
    {
        // Arrange
        var supervisor = CreateSupervisor();

        // Act & Assert — should not throw
        await supervisor.HandleAsync(new RemoveChild(null));
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
    }

    #endregion

    #region AddChild Override

    [Test]
    public async Task AddChild_WithSpec_Should_ReturnLocalActorReference()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var spec = new ChildSpecification(typeof(SimpleActor));

        // Act
        var actorRef = supervisor.AddChild(spec);

        // Assert
        await Assert.That(actorRef).IsNotNull();
        await Assert.That(actorRef).IsTypeOf<LocalActorReference>();
    }

    [Test]
    public async Task AddChild_WithSpec_Should_SendAddActorToSelf()
    {
        // Arrange
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);
        var spec = new ChildSpecification(typeof(SimpleActor));

        // Act
        supervisor.AddChild(spec);

        // Assert — verify AddActor message was enqueued to self
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<AddActor>();
            var addActor = (AddActor)msg.Payload;
            await Assert.That(addActor.Specification).IsSameReferenceAs(spec);
            break;
        }
    }

    [Test]
    public async Task AddChild_WithType_Should_SendAddActorToSelf()
    {
        // Arrange
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);

        // Act
        supervisor.AddChild(typeof(SimpleActor));

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<AddActor>();
            var addActor = (AddActor)msg.Payload;
            await Assert.That(addActor.Specification.ActorType).IsEqualTo(typeof(SimpleActor));
            break;
        }
    }

    [Test]
    public async Task AddChild_Should_WorkAfterInitialization()
    {
        // Arrange — DynamicSupervisor allows adding children after init
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);
        await supervisor.InitializeAsync();

        // Act — should NOT throw (unlike base Supervisor)
        var actorRef = supervisor.AddChild(new ChildSpecification(typeof(SimpleActor)));

        // Assert
        await Assert.That(actorRef).IsNotNull();
    }

    #endregion

    #region AddChildAsync Override

    [Test]
    public async Task AddChildAsync_WithSpec_Should_ReturnLocalActorReference()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var spec = new ChildSpecification(typeof(SimpleActor));

        // Act
        var actorRef = await supervisor.AddChildAsync(spec);

        // Assert
        await Assert.That(actorRef).IsNotNull();
        await Assert.That(actorRef).IsTypeOf<LocalActorReference>();
    }

    [Test]
    public async Task AddChildAsync_WithSpec_Should_SendAddActorToSelf()
    {
        // Arrange
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);
        var spec = new ChildSpecification(typeof(SimpleActor));

        // Act
        await supervisor.AddChildAsync(spec);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<AddActor>();
            break;
        }
    }

    [Test]
    public async Task AddChildAsync_WithType_Should_SendAddActorToSelf()
    {
        // Arrange
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);

        // Act
        await supervisor.AddChildAsync(typeof(SimpleActor));

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<AddActor>();
            var addActor = (AddActor)msg.Payload;
            await Assert.That(addActor.Specification.ActorType).IsEqualTo(typeof(SimpleActor));
            break;
        }
    }

    #endregion

    #region OnActorFailedAsync Override

    [Test]
    public async Task OnActorFailed_TemporaryActor_Should_RemoveFromChildren_DisposeAndNull()
    {
        // Arrange
        var factory = CreateFactory<DisposableActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Temporary,
            },
            reference
        );
        var child = supervisor.Children[0];
        var actor = (DisposableActor)child.Actor;

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test"), new Exception("fail"))
        );

        // Assert — removed from children, actor disposed, refs nulled
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
        await Assert.That(actor.AsyncDisposed).IsTrue();
        await Assert.That(child.Actor).IsNull();
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task OnActorFailed_PermanentActor_Should_NotRemoveFromChildren()
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
                RestartPolicy = RestartPolicy.Permanent,
            },
            reference
        );
        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test"), new Exception("fail"))
        );

        // Assert — still in children (restarted by base)
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OnActorFailed_TransientActor_Should_NotRemoveFromChildren()
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

        // Act
        await supervisor.HandleAsync(
            (object)
                new ActorFailed(child.Actor, new LocalTellMessage("test"), new Exception("fail"))
        );

        // Assert — still in children (restarted by base)
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
    }

    #endregion

    #region OnActorTerminatedAsync Override

    [Test]
    public async Task OnActorTerminated_TransientActor_Should_RemoveFromChildren_DisposeAndNull()
    {
        // Arrange
        var factory = CreateFactory<DisposableActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Transient,
            },
            reference
        );
        var child = supervisor.Children[0];
        var actor = (DisposableActor)child.Actor;

        // Act
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
        await Assert.That(actor.AsyncDisposed).IsTrue();
        await Assert.That(child.Actor).IsNull();
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task OnActorTerminated_TemporaryActor_Should_RemoveFromChildren_DisposeAndNull()
    {
        // Arrange
        var factory = CreateFactory<DisposableActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor))
            {
                Mailbox = mailbox,
                RestartPolicy = RestartPolicy.Temporary,
            },
            reference
        );
        var child = supervisor.Children[0];
        var actor = (DisposableActor)child.Actor;

        // Act
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
        await Assert.That(actor.AsyncDisposed).IsTrue();
        await Assert.That(child.Actor).IsNull();
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task OnActorTerminated_PermanentActor_Should_NotRemoveFromChildren()
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
                RestartPolicy = RestartPolicy.Permanent,
            },
            reference
        );
        var child = supervisor.Children[0];

        // Act
        await supervisor.HandleAsync((object)new ActorTerminated(child.Actor, "done"));

        // Assert — still in children (restarted by base)
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
    }

    #endregion

    #region RemoveActor

    [Test]
    public async Task RemoveActor_Should_SendRemoveChildToSelf()
    {
        // Arrange
        var factory = CreateFactory<SimpleActor>();
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(factory: factory, selfMailbox: selfMailbox);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];

        // Act
        supervisor.RemoveActor(reference);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<RemoveChild>();
            var removeChild = (RemoveChild)msg.Payload;
            await Assert.That(removeChild.Actor).IsSameReferenceAs(child.Actor);
            break;
        }
    }

    [Test]
    public async Task RemoveActor_UnknownReference_Should_NoOp()
    {
        // Arrange
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(selfMailbox: selfMailbox);
        var unknownRef = new LocalActorReference(new ChannelMailbox());

        // Act
        supervisor.RemoveActor(unknownRef);

        // Assert — no message sent to self
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var messageReceived = false;
        try
        {
            await foreach (var _ in selfMailbox.WithCancellation(cts.Token))
            {
                messageReceived = true;
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — no message was sent
        }

        await Assert.That(messageReceived).IsFalse();
    }

    #endregion

    #region RemoveActorAsync

    [Test]
    public async Task RemoveActorAsync_Should_SendRemoveChildToSelf()
    {
        // Arrange
        var factory = CreateFactory<SimpleActor>();
        var selfMailbox = new ChannelMailbox();
        var supervisor = CreateSupervisor(factory: factory, selfMailbox: selfMailbox);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(SimpleActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];

        // Act
        await supervisor.RemoveActorAsync(reference);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await foreach (var msg in selfMailbox.WithCancellation(cts.Token))
        {
            await Assert.That(msg.Payload).IsTypeOf<RemoveChild>();
            var removeChild = (RemoveChild)msg.Payload;
            await Assert.That(removeChild.Actor).IsSameReferenceAs(child.Actor);
            break;
        }
    }

    [Test]
    public async Task RemoveActorAsync_UnknownReference_Should_ReturnCompleted()
    {
        // Arrange
        var supervisor = CreateSupervisor();
        var unknownRef = new LocalActorReference(new ChannelMailbox());

        // Act & Assert — should complete without throwing
        await supervisor.RemoveActorAsync(unknownRef);
    }

    #endregion

    #region Integration

    [Test]
    public async Task Integration_RemoveChild_Should_FullyCleanupRunningActor()
    {
        // Arrange
        var factory = CreateFactory<DisposableActor>();
        var supervisor = CreateSupervisor(factory: factory);
        var mailbox = new ChannelMailbox();
        var reference = new LocalActorReference(mailbox);
        supervisor.CreateActor(
            new ChildSpecification(typeof(DisposableActor)) { Mailbox = mailbox },
            reference
        );
        var child = supervisor.Children[0];
        var actor = (DisposableActor)child.Actor;
        await Task.Delay(50);
        await Assert.That(child.Process.IsRunning).IsTrue();

        // Act
        await supervisor.HandleAsync(new RemoveChild(child.Actor));

        // Assert — fully cleaned up
        await Assert.That(supervisor.Children.Count).IsEqualTo(0);
        await Assert.That(actor.AsyncDisposed).IsTrue();
        await Assert.That(child.Actor).IsNull();
        await Assert.That(child.Process).IsNull();
    }

    [Test]
    public async Task Integration_RemoveOneChild_Should_NotAffectOthers()
    {
        // Arrange
        var factory = Substitute.For<IActorFactory>();
        factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
        var supervisor = CreateSupervisor(factory: factory);

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

        var childToRemove = supervisor.Children[0];
        var childToKeep = supervisor.Children[1];

        // Act
        await supervisor.HandleAsync(new RemoveChild(childToRemove.Actor));

        // Assert
        await Assert.That(supervisor.Children.Count).IsEqualTo(1);
        await Assert.That(supervisor.Children[0]).IsSameReferenceAs(childToKeep);
        await Assert.That(childToKeep.Process.IsRunning).IsTrue();
    }

    #endregion
}
