// using System;
// using System.Collections.Immutable;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using NSubstitute;
// using Trupe.Abstractions;
// using Trupe.Abstractions.Events;
// using Trupe.Abstractions.Exceptions;
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
// public class SupervisorTest
// {
//     #region Test Helpers
//
//     private class TestSupervisor(
//         ILogger logger,
//         Strategy? strategy = null,
//         int? maxRestarts = null,
//         TimeSpan? restartWindow = null,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     ) : Supervisor(logger)
//     {
//         private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;
//
//         private readonly Strategy _strategy = strategy ?? Strategy.OneForOne;
//         private readonly int _maxRestarts = maxRestarts ?? 3;
//         private readonly TimeSpan _restartWindow = restartWindow ?? TimeSpan.FromSeconds(5);
//
//         protected override Strategy Strategy => _strategy;
//         protected override int MaxRestarts => _maxRestarts;
//         protected override TimeSpan RestartWindow => _restartWindow;
//
//         protected override ValueTask OnInitializeAsync(
//             CancellationToken cancellationToken = default
//         )
//         {
//             if (_onInitialize != null)
//             {
//                 return _onInitialize(cancellationToken);
//             }
//
//             return ValueTask.CompletedTask;
//         }
//
//         // Expose protected members for testing
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
//         public new void ResetCounter(Child child) => base.ResetCounter(child);
//
//         public new FailureAction GetFailureAction(Child child, Exception exception) =>
//             base.ResolveFailureAction(child, exception);
//
//         public new Task StopAsync(Child child) => base.StopAsync(child);
//
//         public new Task ResumeActorAsync(Child child) => base.ResumeActorAsync(child);
//
//         public new Task EscalateAsync(Child child, IMessage message, Exception exception) =>
//             base.EscalateAsync(child, message, exception);
//
//         public new Task RestartAsync(Child child) => base.RestartAsync(child);
//
//         public new Child CreateActor(IChildSpecification specification) =>
//             base.CreateActor(specification);
//
//         public new Task StartActorAsync(Child child) => base.StartActorAsync(child);
//
//         public new ValueTask DisposeObjectAsync(object obj) => base.DisposeObjectAsync(obj);
//
//         public new ValueTask<IMailbox> GetOrCreateMailboxAsync(Child child) =>
//             base.GetOrCreateMailboxAsync(child);
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
//         public bool BeforeRestartCalled { get; set; }
//
//         public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
//         {
//             Initialized = true;
//             return ValueTask.CompletedTask;
//         }
//
//         public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
//         {
//             BeforeRestartCalled = true;
//             return ValueTask.CompletedTask;
//         }
//     }
//
//     private class SimpleSupervisorActor : Actor, ISupervisor
//     {
//         public System.Collections.Generic.IEnumerable<IActorReference> Children =>
//             Enumerable.Empty<IActorReference>();
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
//     private class SyncDisposableActor : Actor, IDisposable
//     {
//         public bool SyncDisposed { get; set; }
//
//         public void Dispose()
//         {
//             SyncDisposed = true;
//         }
//     }
//
//     private static TestSupervisor CreateSupervisor(
//         IActorFactory? factory = null,
//         Strategy? strategy = null,
//         int? maxRestarts = null,
//         TimeSpan? restartWindow = null,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     )
//     {
//         factory ??= Substitute.For<IActorFactory>();
//         var logger = Substitute.For<ILogger>();
//         var supervisor = new TestSupervisor(
//             logger,
//             strategy,
//             maxRestarts,
//             restartWindow,
//             onInitialize
//         );
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
//         var selfRef = refFactory.Create("test-supervisor", process);
//
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         return supervisor;
//     }
//
//     private static Child CreateChild(
//         TestSupervisor supervisor,
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
//         TestSupervisor supervisor,
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
//         await Task.Delay(50); // Allow initialization message to be processed
//         return child;
//     }
//
//     private static IActorFactory CreateFactory(IActor actorToReturn)
//     {
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(actorToReturn);
//         return factory;
//     }
//
//     #endregion
//
//     #region Initialization
//
//     [Test]
//     public async Task InitializeAsync_Should_CallOnInitializeAsync()
//     {
//         // Arrange
//         var called = false;
//         var supervisor = CreateSupervisor(onInitialize: _ =>
//         {
//             called = true;
//             return ValueTask.CompletedTask;
//         });
//
//         // Act
//         await supervisor.InitializeAsync();
//
//         // Assert
//         await Assert.That(called).IsTrue();
//     }
//
//     [Test]
//     public async Task InitializeAsync_Should_MarkAsInitialized_PreventingAddChild()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//
//         // Act
//         await supervisor.InitializeAsync();
//
//         // Assert
//         var action = () => supervisor.AddChild(new ChildSpecification(typeof(SimpleActor)));
//         await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
//     }
//
//     #endregion
//
//     #region Message Routing
//
//     [Test]
//     public async Task HandleAsync_Should_RouteAddActorMessage()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
//         var message = new AddActor(child);
//
//         // Act
//         await supervisor.HandleAsync((object)message);
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//     }
//
//     [Test]
//     public async Task HandleAsync_Should_RouteActorProcessFailedMessage()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//
//         var tellMessage = new TellMessage("test", []);
//         var failedMessage = new ActorProcessFailed(
//             child.Process,
//             tellMessage,
//             new InvalidOperationException("test")
//         );
//
//         // Act - should not throw (actor found, restart applied)
//         await supervisor.HandleAsync((object)failedMessage);
//
//         // Assert - child was restarted so factory called to recreate
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleAsync_Should_RouteActorProcessStoppedMessage()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//
//         var stoppedMessage = new ActorProcessStopped(child.Process, TerminatedReason.Stopped);
//
//         // Act - permanent actor should be reset
//         await supervisor.HandleAsync((object)stoppedMessage);
//
//         // Assert - factory called to recreate
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleAsync_Should_RouteUnknownMessageToBase()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//
//         // Act & Assert - base HandleAsync throws for unhandled messages
//         var action = async () => await supervisor.HandleAsync((object)"unknown message");
//         await Assert.That(action).ThrowsException();
//     }
//
//     #endregion
//
//     #region Adding Children
//
//     [Test]
//     public async Task AddChild_WithType_Should_WorkBeforeInitialization()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//
//         // Act
//         var actorRef = supervisor.AddChild(typeof(SimpleActor));
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     [Test]
//     public async Task AddChild_WithSpec_Should_WorkBeforeInitialization()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = RestartPolicy.Transient,
//         };
//
//         // Act
//         var actorRef = supervisor.AddChild(spec);
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     [Test]
//     public async Task AddChild_Should_ThrowAfterInitialization()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         await supervisor.InitializeAsync();
//
//         // Act & Assert
//         var action = () => supervisor.AddChild(typeof(SimpleActor));
//         await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
//     }
//
//     [Test]
//     public async Task AddChildAsync_WithType_Should_WorkBeforeInitialization()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//
//         // Act
//         var actorRef = await supervisor.AddChildAsync(typeof(SimpleActor));
//
//         // Assert
//         await Assert.That(actorRef).IsNotNull();
//     }
//
//     [Test]
//     public async Task AddChildAsync_WithSpec_Should_WorkBeforeInitialization()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
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
//     public async Task AddChildAsync_Should_ThrowAfterInitialization()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         await supervisor.InitializeAsync();
//
//         // Act & Assert
//         var action = async () => await supervisor.AddChildAsync(typeof(SimpleActor));
//         await Assert.That(action).Throws<SupervisorAlreadyInitializedException>();
//     }
//
//     #endregion
//
//     #region Creating Actors
//
//     [Test]
//     public async Task CreateActor_Should_CreateViaFactory_SetContext_AddToChildren()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//         supervisor.Children = supervisor.Children.Add(child);
//         await supervisor.StartActorAsync(child);
//         await Task.Delay(50);
//
//         // Assert
//         await Assert.That(supervisor.Children).Count().IsEqualTo(1);
//         await Assert.That(child.Actor).IsSameReferenceAs(childActor);
//         await Assert.That(childActor.Context).IsNotNull();
//         factory.Received(1).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task CreateActor_Should_StartProcess_WithInitializeMessage()
//     {
//         // Arrange
//         var childActor = new SimpleActor();
//         var factory = CreateFactory(childActor);
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//         await supervisor.StartActorAsync(child);
//         await Task.Delay(100); // Allow initialization message to be processed
//
//         // Assert
//         await Assert.That(childActor.Initialized).IsTrue();
//     }
//
//     [Test]
//     public async Task CreateActor_Should_TrackMultipleChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//
//         // Act
//         var child1 = supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
//         var child2 = supervisor.CreateActor(new ChildSpecification(typeof(SimpleActor)));
//         supervisor.Children = supervisor.Children.Add(child1).Add(child2);
//
//         // Assert
//         await Assert.That(supervisor.Children).Count().IsEqualTo(2);
//     }
//
//     [Test]
//     public async Task CreateActor_Should_PropagateRestartPolicy_Permanent()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = RestartPolicy.Permanent,
//         };
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//
//         // Assert
//         await Assert.That(child.RestartPolicy).IsEqualTo(RestartPolicy.Permanent);
//     }
//
//     [Test]
//     public async Task CreateActor_Should_PropagateRestartPolicy_Transient()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = RestartPolicy.Transient,
//         };
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//
//         // Assert
//         await Assert.That(child.RestartPolicy).IsEqualTo(RestartPolicy.Transient);
//     }
//
//     [Test]
//     public async Task CreateActor_Should_PropagateRestartPolicy_Temporary()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor))
//         {
//             RestartPolicy = RestartPolicy.Temporary,
//         };
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//
//         // Assert
//         await Assert.That(child.RestartPolicy).IsEqualTo(RestartPolicy.Temporary);
//     }
//
//     [Test]
//     public async Task CreateActor_Should_DefaultRestartPolicy_Permanent()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//
//         // Assert - ChildSpecification defaults to Permanent
//         await Assert.That(child.RestartPolicy).IsEqualTo(RestartPolicy.Permanent);
//     }
//
//     #endregion
//
//     #region Actor Failure Handling
//
//     [Test]
//     public async Task HandleActorFailed_PermanentActor_OneForOne_Should_RestartActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert - factory called twice: initial + restart
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_PermanentActor_AllForOne_Should_RestartAllActors()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child1.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert - factory called 4 times: 2 initial + 2 restart
//         factory.Received(4).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_TemporaryActor_Should_StopWithoutRestart()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, restartPolicy: RestartPolicy.Temporary);
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert - factory called only once (initial), no restart
//         factory.Received(1).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_TransientActor_Should_RestartOnFailure()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, restartPolicy: RestartPolicy.Transient);
//         var tellMessage = new TellMessage("test", []);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, tellMessage, new Exception("fail"))
//         );
//
//         // Assert - transient actors are restarted on failure (factory called twice: initial + restart)
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_WithAskMessage_Should_CancelAskMessage()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var askMessage = new AskMessage("test", [], CancellationToken.None);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, askMessage, new Exception("fail"))
//         );
//
//         // Assert - ask message should be canceled
//         var action = async () => await askMessage.AsTask();
//         await Assert.That(action).Throws<TaskCanceledException>();
//     }
//
//     [Test]
//     public async Task HandleActorFailed_WithEscalateExceptionContainingAskMessage_Should_CancelNestedAskMessage()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var nestedAskMessage = new AskMessage("nested", [], CancellationToken.None);
//         var tellMessage = new TellMessage("test", []);
//
//         var escalateException = new EscalateFailureException(
//             "escalated",
//             child.Reference,
//             nestedAskMessage,
//             new Exception("inner")
//         );
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, tellMessage, escalateException)
//         );
//
//         // Assert
//         var action = async () => await nestedAskMessage.AsTask();
//         await Assert.That(action).Throws<TaskCanceledException>();
//     }
//
//     [Test]
//     public async Task HandleActorFailed_UnknownActor_Should_ReturnWithoutAction()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var unknownProcess = Substitute.For<IActorProcess>();
//         var tellMessage = new TellMessage("test", []);
//
//         // Act & Assert - should not throw
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(unknownProcess, tellMessage, new Exception("fail"))
//         );
//     }
//
//     [Test]
//     public async Task HandleActorFailed_ExceedsMaxRestarts_Should_Escalate()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             maxRestarts: 1,
//             restartWindow: TimeSpan.FromMinutes(10)
//         );
//         var child = await CreateAndStartChild(supervisor);
//         var tellMessage = new TellMessage("test", []);
//
//         // First failure - restart
//         await supervisor.HandleAsync(
//             new ActorProcessFailed(child.Process, tellMessage, new Exception("fail1"))
//         );
//
//         // Get the updated child after restart
//         var restartedChild = supervisor.Children[0];
//
//         // Second failure - should escalate
//         var action = async () =>
//             await supervisor.HandleAsync(
//                 new ActorProcessFailed(
//                     restartedChild.Process,
//                     tellMessage,
//                     new Exception("fail2")
//                 )
//             );
//
//         await Assert.That(action).Throws<EscalateFailureException>();
//     }
//
//     #endregion
//
//     #region Actor Termination Handling
//
//     [Test]
//     public async Task HandleActorStopped_PermanentActor_Should_ResetActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert - factory called twice: initial + reset
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorStopped_TransientActor_Should_TerminateReference()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, restartPolicy: RestartPolicy.Transient);
//
//         var terminateCalled = false;
//         child.Reference.Terminated += (_, _) => terminateCalled = true;
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert - not restarted, terminate event raised
//         await Assert.That(terminateCalled).IsTrue();
//     }
//
//     [Test]
//     public async Task HandleActorStopped_TemporaryActor_Should_TerminateReference()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, restartPolicy: RestartPolicy.Temporary);
//
//         var terminateCalled = false;
//         child.Reference.Terminated += (_, _) => terminateCalled = true;
//
//         // Act
//         await supervisor.HandleAsync(
//             new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert
//         await Assert.That(terminateCalled).IsTrue();
//     }
//
//     [Test]
//     public async Task HandleActorStopped_UnknownActor_Should_ReturnWithoutAction()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var unknownProcess = Substitute.For<IActorProcess>();
//
//         // Act & Assert - should not throw
//         await supervisor.HandleAsync(
//             new ActorProcessStopped(unknownProcess, TerminatedReason.Stopped)
//         );
//     }
//
//     #endregion
//
//     #region Restart Counter
//
//     [Test]
//     public async Task ResetCounter_Should_ResetWhenWindowElapsed()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(restartWindow: TimeSpan.FromMilliseconds(1));
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var child = CreateChild(supervisor);
//         child.RestartCount = 5;
//         child.LastRestartTime = DateTimeOffset.UtcNow.AddSeconds(-10);
//
//         // Act
//         supervisor.ResetCounter(child);
//
//         // Assert
//         await Assert.That(child.RestartCount).IsEqualTo(0);
//     }
//
//     [Test]
//     public async Task ResetCounter_Should_PreserveCountWithinWindow()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(restartWindow: TimeSpan.FromMinutes(10));
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var child = CreateChild(supervisor);
//         child.RestartCount = 2;
//         child.LastRestartTime = DateTimeOffset.UtcNow;
//
//         // Act
//         supervisor.ResetCounter(child);
//
//         // Assert
//         await Assert.That(child.RestartCount).IsEqualTo(2);
//     }
//
//     #endregion
//
//     #region Failure Action
//
//     [Test]
//     public async Task GetFailureAction_Should_ReturnRestart_WhenBelowMaxRestarts()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(maxRestarts: 3);
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var child = CreateChild(supervisor);
//         child.RestartCount = 1;
//
//         // Act
//         var action = supervisor.GetFailureAction(child, new Exception("fail"));
//
//         // Assert
//         await Assert.That(action).IsEqualTo(FailureAction.Restart);
//     }
//
//     [Test]
//     public async Task GetFailureAction_Should_ReturnEscalate_WhenAtMaxRestarts()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(maxRestarts: 3);
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var child = CreateChild(supervisor);
//         child.RestartCount = 3;
//
//         // Act
//         var action = supervisor.GetFailureAction(child, new Exception("fail"));
//
//         // Assert
//         await Assert.That(action).IsEqualTo(FailureAction.Escalate);
//     }
//
//     [Test]
//     public async Task GetFailureAction_Should_ReturnEscalate_WhenAboveMaxRestarts()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(maxRestarts: 3);
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var child = CreateChild(supervisor);
//         child.RestartCount = 5;
//
//         // Act
//         var action = supervisor.GetFailureAction(child, new Exception("fail"));
//
//         // Assert
//         await Assert.That(action).IsEqualTo(FailureAction.Escalate);
//     }
//
//     #endregion
//
//     #region Apply Actions
//
//     [Test]
//     public async Task StopAsync_OneForOne_Should_StopOnlyFailedActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.OneForOne);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.StopAsync(child1);
//
//         // Assert - child1 terminated, child2 still has reference
//         await Assert.That(supervisor.Children.Count).IsEqualTo(2);
//     }
//
//     [Test]
//     public async Task StopAsync_AllForOne_Should_StopAllChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.StopAsync(child1);
//
//         // Assert - both children stopped
//         await Assert.That(supervisor.Children.Count).IsEqualTo(2);
//     }
//
//     [Test]
//     public async Task ResumeActorAsync_Should_RestartProcess()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//
//         // Act - should not throw
//         await supervisor.ResumeActorAsync(child);
//
//         // Assert - process was restarted (no exception)
//         await Assert.That(child.Process).IsNotNull();
//     }
//
//     [Test]
//     public async Task EscalateAsync_Should_ThrowEscalateFailureException()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var tellMessage = new TellMessage("test", []);
//         var innerException = new InvalidOperationException("inner");
//
//         // Act & Assert
//         var action = async () =>
//             await supervisor.EscalateAsync(child, tellMessage, innerException);
//         await Assert.That(action).Throws<EscalateFailureException>();
//     }
//
//     [Test]
//     public async Task RestartAsync_OneForOne_Should_ResetOnlyFailedActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.OneForOne);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//         var originalActor = child1.Actor;
//
//         // Act
//         await supervisor.RestartAsync(child1);
//
//         // Assert - restart count incremented, actor replaced
//         await Assert.That(child1.RestartCount).IsEqualTo(1);
//         await Assert.That(child1.Actor).IsNotSameReferenceAs(originalActor);
//         // factory: 2 initial + 1 restart
//         factory.Received(3).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task RestartAsync_AllForOne_Should_ResetAllChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory, strategy: Strategy.AllForOne);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//
//         // Act
//         await supervisor.RestartAsync(child1);
//
//         // Assert - factory: 2 initial + 2 restart
//         factory.Received(4).CreateActor(typeof(SimpleActor));
//         await Assert.That(child1.RestartCount).IsEqualTo(1);
//     }
//
//     #endregion
//
//     #region Actor Reset
//
//     [Test]
//     public async Task ResetActorAsync_Should_RecreateActorViaFactory()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var originalActor = child.Actor;
//
//         // Act
//         await supervisor.RestartAsync(child);
//
//         // Assert
//         await Assert.That(child.Actor).IsNotSameReferenceAs(originalActor);
//         await Assert.That(child.Actor.Context).IsNotNull();
//     }
//
//     [Test]
//     public async Task ResetActorAsync_Should_CallBeforeRestartOnOldActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var originalActor = (SimpleActor)child.Actor;
//
//         // Act
//         await supervisor.RestartAsync(child);
//
//         // Assert
//         await Assert.That(originalActor.BeforeRestartCalled).IsTrue();
//     }
//
//     #endregion
//
//     #region BeforeRestart
//
//     [Test]
//     public async Task BeforeRestartActorAsync_Should_CallBeforeRestartOnActor()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var actorBeforeRestart = (SimpleActor)child.Actor;
//
//         // Act
//         await supervisor.RestartAsync(child);
//
//         // Assert
//         await Assert.That(actorBeforeRestart.BeforeRestartCalled).IsTrue();
//     }
//
//     [Test]
//     public async Task BeforeRestartActorAsync_Should_SwallowException()
//     {
//         // Arrange
//         var actor = Substitute.For<IActor>();
//         actor
//             .BeforeRestartAsync(Arg.Any<CancellationToken>())
//             .Returns(ValueTask.FromException(new InvalidOperationException("boom")));
//
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(actor, new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor, actor: actor);
//
//         // Act & Assert - should not throw
//         await supervisor.RestartAsync(child);
//     }
//
//     #endregion
//
//     #region Event Handlers
//
//     [Test]
//     public async Task OnActorProcessFailed_Should_SendActorProcessFailedToSelf()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var registry = sp.GetRequiredService<IActorProcessRegistry>();
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-supervisor-events", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var childProcess = Substitute.For<IActorProcess>();
//         var tellMessage = new TellMessage("test", []);
//         var exception = new Exception("fail");
//         var args = new ActorProcessFailedEvetArgs(childProcess, tellMessage, exception);
//
//         // Act
//         supervisor.OnActorProcessFailed(null, args);
//
//         // Assert - message should have been enqueued to supervisor's mailbox
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<ActorProcessFailed>();
//         var failed = (ActorProcessFailed)msg.Payload;
//         await Assert.That(failed.Process).IsSameReferenceAs(childProcess);
//         await Assert.That(failed.Exception).IsSameReferenceAs(exception);
//     }
//
//     [Test]
//     public async Task OnActorProcessStopped_Should_SendActorProcessStoppedToSelf()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var registry = sp.GetRequiredService<IActorProcessRegistry>();
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-supervisor-term", process);
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         var childProcess = Substitute.For<IActorProcess>();
//         var args = new ActorProcessStoppedEventArgs(childProcess, TerminatedReason.Stopped);
//
//         // Act
//         supervisor.OnActorProcessStopped(null, args);
//
//         // Assert
//         var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
//         var msg = await supervisorMailbox.DequeueAsync(cts.Token);
//         await Assert.That(msg!.Payload).IsTypeOf<ActorProcessStopped>();
//         var stopped = (ActorProcessStopped)msg.Payload;
//         await Assert.That(stopped.Process).IsSameReferenceAs(childProcess);
//         await Assert.That(stopped.Reason).IsEqualTo(TerminatedReason.Stopped);
//     }
//
//     #endregion
//
//     #region Disposal
//
//     [Test]
//     public async Task DisposeAsync_Should_DisposeAllChildrenAndClearList()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//         var child = await CreateAndStartChild(supervisor);
//         var disposableActor = (DisposableActor)child.Actor;
//
//         // Act
//         await supervisor.DisposeAsync();
//
//         // Assert
//         await Assert.That(disposableActor.AsyncDisposed).IsTrue();
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//     }
//
//     [Test]
//     public async Task DisposeObjectAsync_Should_CallDisposeAsync_ForAsyncDisposable()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var disposable = new DisposableActor();
//
//         // Act
//         await supervisor.DisposeObjectAsync(disposable);
//
//         // Assert
//         await Assert.That(disposable.AsyncDisposed).IsTrue();
//     }
//
//     [Test]
//     public async Task DisposeObjectAsync_Should_CallDispose_ForSyncDisposable()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var disposable = new SyncDisposableActor();
//
//         // Act
//         await supervisor.DisposeObjectAsync(disposable);
//
//         // Assert
//         await Assert.That(disposable.SyncDisposed).IsTrue();
//     }
//
//     [Test]
//     public async Task DisposeObjectAsync_Should_NoOp_ForNonDisposable()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor();
//         var obj = new SimpleActor();
//
//         // Act & Assert - should not throw
//         await supervisor.DisposeObjectAsync(obj);
//     }
//
//     #endregion
//
//     #region Supervisor BeforeRestart
//
//     [Test]
//     public async Task BeforeRestartAsync_Should_StopAndDisposeAllChildren()
//     {
//         // Arrange
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new DisposableActor());
//         var supervisor = CreateSupervisor(factory: factory);
//
//         var child1 = await CreateAndStartChild(supervisor);
//         var child2 = await CreateAndStartChild(supervisor);
//         var actor1 = (DisposableActor)child1.Actor;
//         var actor2 = (DisposableActor)child2.Actor;
//
//         // Act
//         await supervisor.BeforeRestartAsync();
//
//         // Assert
//         await Assert.That(actor1.AsyncDisposed).IsTrue();
//         await Assert.That(actor2.AsyncDisposed).IsTrue();
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//     }
//
//     #endregion
// }
