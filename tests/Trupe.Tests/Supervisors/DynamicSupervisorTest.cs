// using System;
// using System.Collections.Immutable;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using NSubstitute;
// using Trupe.Abstractions;
// using Trupe.Abstractions.Events;
// using Trupe.Abstractions.Mailboxes;
// using Trupe.Abstractions.Messages;
// using Trupe.Abstractions.Supervisors;
// using Trupe.Abstractions.Supervisors.Commands;
// using Trupe.Extensions;
// using Trupe.Mailboxes;
// using Trupe.Messages;
// using Trupe.Supervisors;
// using Trupe.Supervisors.Commands;
//
// namespace Trupe.Tests.Supervisors;
//
// public class DynamicSupervisorTest
// {
//     #region Test Helpers
//
//     private class TestDynamicSupervisor(
//         ILogger logger,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     ) : DynamicSupervisor(logger)
//     {
//         private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;
//
//         protected override ValueTask OnInitializeAsync(
//             CancellationToken cancellationToken = default
//         )
//         {
//             if (_onInitialize != null)
//                 return _onInitialize(cancellationToken);
//             return ValueTask.CompletedTask;
//         }
//
//         public Strategy TestableStrategy => Strategy;
//
//         public new ImmutableList<Child> Children
//         {
//             get => base.Children;
//             set => base.Children = value;
//         }
//
//         public new IActorReference AddChild(IChildSpecification specification) =>
//             base.AddChild(specification);
//
//         public new IActorReference AddChild(Type actorType) => base.AddChild(actorType);
//
//         public new ValueTask<IActorReference> AddChildAsync(
//             IChildSpecification specification,
//             CancellationToken cancellationToken = default
//         ) => base.AddChildAsync(specification, cancellationToken);
//
//         public new ValueTask<IActorReference> AddChildAsync(
//             Type actorType,
//             CancellationToken cancellationToken = default
//         ) => base.AddChildAsync(actorType, cancellationToken);
//
//         public new Child CreateActor(IChildSpecification specification) =>
//             base.CreateActor(specification);
//
//         public new Task StartActorAsync(Child child) => base.StartActorAsync(child);
//
//         public new void RemoveActor(IActorReference reference) => base.RemoveActor(reference);
//
//         public new ValueTask RemoveActorAsync(
//             IActorReference reference,
//             CancellationToken cancellationToken = default
//         ) => base.RemoveActorAsync(reference, cancellationToken);
//
//         public new void OnActorProcessFailed(object? sender, ActorProcessFailedEvetArgs args) =>
//             base.OnActorProcessFailed(sender, args);
//
//         public new void OnActorProcessStopped(
//             object? sender,
//             ActorProcessStoppedEventArgs args
//         ) => base.OnActorProcessStopped(sender, args);
//     }
//
//     private class SimpleActor : Actor
//     {
//         public bool Initialized { get; set; }
//
//         public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
//         {
//             Initialized = true;
//             return ValueTask.CompletedTask;
//         }
//     }
//
//     private class DisposableActor : Actor, IAsyncDisposable
//     {
//         public bool AsyncDisposed { get; set; }
//
//         public ValueTask DisposeAsync()
//         {
//             AsyncDisposed = true;
//             return ValueTask.CompletedTask;
//         }
//     }
//
//     private static TestDynamicSupervisor CreateSupervisor(
//         IActorFactory? factory = null,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     )
//     {
//         factory ??= Substitute.For<IActorFactory>();
//         var logger = Substitute.For<ILogger>();
//         var supervisor = new TestDynamicSupervisor(logger, onInitialize);
//
//         var services = new ServiceCollection();
//         services.AddTrupe(c => { });
//         services.AddSingleton(factory);
//         var sp = services.BuildServiceProvider();
//
//         var registry = sp.GetRequiredService<IActorProcessRegistry>();
//         var mailbox = new ChannelMailbox();
//         var process = new ActorProcess(supervisor, mailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor", process);
//
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         return supervisor;
//     }
//
//     private static Child CreateChild(
//         TestDynamicSupervisor supervisor,
//         IActor? actor = null,
//         RestartPolicy restartPolicy = RestartPolicy.Permanent
//     )
//     {
//         actor ??= new SimpleActor();
//         var factory = supervisor.Context.ServiceProvider.GetRequiredService<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(actor);
//
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = restartPolicy,
//         };
//         var child = supervisor.CreateActor(spec);
//         return child;
//     }
//
//     private static async Task<Child> CreateAndStartChild(
//         TestDynamicSupervisor supervisor,
//         IActor? actor = null,
//         RestartPolicy restartPolicy = RestartPolicy.Permanent
//     )
//     {
//         actor ??= new SimpleActor();
//         var factory = supervisor.Context.ServiceProvider.GetRequiredService<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(actor);
//
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = restartPolicy,
//         };
//         var child = supervisor.CreateActor(spec);
//         supervisor.Children = supervisor.Children.Add(child);
//         await supervisor.StartActorAsync(child);
//         await Task.Delay(50);
//         return child;
//     }
//
//     #endregion
//
//     #region Strategy
//
//     [Test]
//     public async Task Strategy_Should_AlwaysBeOneForOne()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//
//         // Assert
//         await Assert.That(supervisor.TestableStrategy).IsEqualTo(Strategy.OneForOne);
//     }
//
//     #endregion
//
//     #region HandleAsync(RemoveChild)
//
//     [Test]
//     public async Task HandleRemoveChild_Should_RemoveFromChildren_StopDispose_NullRefs()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, actor: new DisposableActor());
//         var actor = (DisposableActor)child.Actor;
//
//         // Act
//         await supervisor.HandleAsync(new RemoveChild(child.Actor));
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//         await Assert.That(actor.AsyncDisposed).IsTrue();
//         await Assert.That(child.Actor).IsNull();
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task HandleRemoveChild_Should_StopRunningProcess()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var actorToRemove = child.Actor;
//
//         // Act
//         await supervisor.HandleAsync(new RemoveChild(actorToRemove));
//
//         // Assert
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task HandleRemoveChild_UnknownActor_Should_NoOp()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         await CreateAndStartChild(supervisor);
//
//         // Act — remove an actor that's not a child
//         await supervisor.HandleAsync(new RemoveChild(new SimpleActor()));
//
//         // Assert — children unchanged
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//     }
//
//     [Test]
//     public async Task HandleRemoveChild_NullActor_Should_NoOp()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//
//         // Act & Assert — should not throw
//         await supervisor.HandleAsync(new RemoveChild(null));
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//     }
//
//     #endregion
//
//     #region AddChild Override
//
//     [Test]
//     public async Task AddChild_WithSpec_Should_ReturnActorReference()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var actorRef = supervisor.AddChild(spec);
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     [Test]
//     public async Task AddChild_WithSpec_Should_SendAddActorToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-addchild", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         supervisor.AddChild(spec);
//
//         // Assert — verify AddActor message was enqueued to self
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<AddActor>();
//         var addActor = (AddActor)msg.Payload;
//         await Assert.That(addActor.Child.ActorType).IsEqualTo(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task AddChild_WithType_Should_SendAddActorToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-addchild-type", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         // Act
//         supervisor.AddChild(typeof(SimpleActor));
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<AddActor>();
//         var addActor = (AddActor)msg.Payload;
//         await Assert.That(addActor.Child.ActorType).IsEqualTo(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task AddChild_Should_WorkAfterInitialization()
//     {
//         // Arrange — DynamicSupervisor allows adding children after init
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         await supervisor.InitializeAsync();
//
//         // Act — should NOT throw (unlike base Supervisor)
//         var actorRef = supervisor.AddChild(new ChildSpecification(typeof(SimpleActor)));
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     #endregion
//
//     #region AddChildAsync Override
//
//     [Test]
//     public async Task AddChildAsync_WithSpec_Should_ReturnActorReference()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var actorRef = await supervisor.AddChildAsync(spec);
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     [Test]
//     public async Task AddChildAsync_WithSpec_Should_SendAddActorToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-addchildasync", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         await supervisor.AddChildAsync(spec);
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<AddActor>();
//     }
//
//     [Test]
//     public async Task AddChildAsync_WithType_Should_SendAddActorToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-addchildasync-type", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         // Act
//         await supervisor.AddChildAsync(typeof(SimpleActor));
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<AddActor>();
//         var addActor = (AddActor)msg.Payload;
//         await Assert.That(addActor.Child.ActorType).IsEqualTo(typeof(SimpleActor));
//     }
//
//     #endregion
//
//     #region OnActorFailedAsync Override
//
//     [Test]
//     public async Task OnActorFailed_TemporaryActor_Should_RemoveFromChildren_DisposeAndNull()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             actor: new DisposableActor(),
//             restartPolicy: RestartPolicy.Temporary
//         );
//         var actor = (DisposableActor)child.Actor;
//
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert — removed from children, actor disposed, refs nulled
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//         await Assert.That(actor.AsyncDisposed).IsTrue();
//         await Assert.That(child.Actor).IsNull();
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task OnActorFailed_PermanentActor_Should_NotRemoveFromChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             restartPolicy: RestartPolicy.Permanent
//         );
//
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert — still in children (restarted by base)
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//     }
//
//     [Test]
//     public async Task OnActorFailed_TransientActor_Should_NotRemoveFromChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             restartPolicy: RestartPolicy.Transient
//         );
//
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert — still in children (restarted by base)
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//     }
//
//     #endregion
//
//     #region OnActorTerminatedAsync Override
//
//     [Test]
//     public async Task OnActorTerminated_TransientActor_Should_RemoveFromChildren_DisposeAndNull()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             actor: new DisposableActor(),
//             restartPolicy: RestartPolicy.Transient
//         );
//         var actor = (DisposableActor)child.Actor;
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//         await Assert.That(actor.AsyncDisposed).IsTrue();
//         await Assert.That(child.Actor).IsNull();
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task OnActorTerminated_TemporaryActor_Should_RemoveFromChildren_DisposeAndNull()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             actor: new DisposableActor(),
//             restartPolicy: RestartPolicy.Temporary
//         );
//         var actor = (DisposableActor)child.Actor;
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//         await Assert.That(actor.AsyncDisposed).IsTrue();
//         await Assert.That(child.Actor).IsNull();
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task OnActorTerminated_PermanentActor_Should_NotRemoveFromChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(
//             supervisor,
//             restartPolicy: RestartPolicy.Permanent
//         );
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert — still in children (restarted by base)
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//     }
//
//     #endregion
//
//     #region RemoveActor
//
//     [Test]
//     public async Task RemoveActor_Should_SendRemoveChildToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-remove", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var child = await CreateAndStartChild(supervisor);
//
//         // Act
//         supervisor.RemoveActor(child.Reference);
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<RemoveChild>();
//         var removeChild = (RemoveChild)msg.Payload;
//         await Assert.That(removeChild.Actor).IsSameReferenceAs(child.Actor);
//     }
//
//     [Test]
//     public async Task RemoveActor_UnknownReference_Should_NoOp()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-remove-unknown", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var unknownRef = Substitute.For<IActorReference>();
//
//         // Act
//         supervisor.RemoveActor(unknownRef);
//
//         // Assert — no message sent to self
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
//         var messageReceived = false;
//         try
//         {
//             var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//             if (msg != null)
//                 messageReceived = true;
//         }
//         catch (OperationCanceledException)
//         {
//             // Expected — no message was sent
//         }
//
//         await Assert.That(messageReceived).IsFalse();
//     }
//
//     #endregion
//
//     #region RemoveActorAsync
//
//     [Test]
//     public async Task RemoveActorAsync_Should_SendRemoveChildToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-dynamic-supervisor-removeasync", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var child = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.RemoveActorAsync(child.Reference);
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<RemoveChild>();
//         var removeChild = (RemoveChild)msg.Payload;
//         await Assert.That(removeChild.Actor).IsSameReferenceAs(child.Actor);
//     }
//
//     [Test]
//     public async Task RemoveActorAsync_UnknownReference_Should_ReturnCompleted()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var unknownRef = Substitute.For<IActorReference>();
//
//         // Act & Assert — should complete without throwing
//         await supervisor.RemoveActorAsync(unknownRef);
//     }
//
//     #endregion
//
//     #region Integration
//
//     [Test]
//     public async Task Integration_RemoveChild_Should_FullyCleanupRunningActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, actor: new DisposableActor());
//         var actor = (DisposableActor)child.Actor;
//
//         // Act
//         await supervisor.HandleAsync(new RemoveChild(child.Actor));
//
//         // Assert — fully cleaned up
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//         await Assert.That(actor.AsyncDisposed).IsTrue();
//         await Assert.That(child.Actor).IsNull();
//         await Assert.That(child.Process).IsNull();
//     }
//
//     [Test]
//     public async Task Integration_RemoveOneChild_Should_NotAffectOthers()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.HandleAsync(new RemoveChild(child1.Actor));
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//         await Assert.That(supervisor.Children[0]).IsSameReferenceAs(child2);
//     }
//
//     #endregion
// }
