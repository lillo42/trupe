// using System;
// using System.Collections.Generic;
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
//
// namespace Trupe.Tests.Supervisors;
//
// public class PartitionSupervisorTest
// {
//     #region Test Helpers
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
//     private class SimpleSupervisorActor : Actor, ISupervisor
//     {
//         public IEnumerable<IActorReference> Children => Enumerable.Empty<IActorReference>();
//     }
//
//     private class TestPartitionSupervisor(
//         ILogger logger,
//         int workers = 3,
//         Strategy? strategy = null,
//         int? maxRestarts = null,
//         TimeSpan? restartWindow = null,
//         RestartPolicy? defaultRestartPolicy = null,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     ) : PartitionSupervisor<SimpleActor>(logger, workers)
//     {
//         private readonly Strategy _strategy = strategy ?? Strategy.OneForOne;
//         private readonly int _maxRestarts = maxRestarts ?? 3;
//         private readonly TimeSpan _restartWindow = restartWindow ?? TimeSpan.FromSeconds(5);
//         private readonly RestartPolicy _defaultRestartPolicy =
//             defaultRestartPolicy ?? RestartPolicy.Permanent;
//         private readonly Func<CancellationToken, ValueTask>? _onInitialize = onInitialize;
//
//         protected override Strategy Strategy => _strategy;
//         protected override int MaxRestarts => _maxRestarts;
//         protected override TimeSpan RestartWindow => _restartWindow;
//         protected override RestartPolicy DefaultRestartPolicy => _defaultRestartPolicy;
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
//         // Expose protected members for testing
//         public new ImmutableList<Child> Children
//         {
//             get => base.Children;
//             set => base.Children = value;
//         }
//
//         public new Child CreateActor(IChildSpecification specification) =>
//             base.CreateActor(specification);
//
//         public new Task StartActorAsync(Child child) => base.StartActorAsync(child);
//
//         public new IActorReference GetActorReference<TKey>(TKey key)
//             where TKey : notnull => base.GetActorReference(key);
//
//         public new int GetHashcode<TKey>(TKey key)
//             where TKey : notnull => base.GetHashcode(key);
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
//     private static TestPartitionSupervisor CreateSupervisor(
//         IActorFactory? factory = null,
//         int workers = 3,
//         Strategy? strategy = null,
//         int? maxRestarts = null,
//         TimeSpan? restartWindow = null,
//         RestartPolicy? defaultRestartPolicy = null,
//         Func<CancellationToken, ValueTask>? onInitialize = null
//     )
//     {
//         factory ??= Substitute.For<IActorFactory>();
//         var logger = Substitute.For<ILogger>();
//         var supervisor = new TestPartitionSupervisor(
//             logger,
//             workers,
//             strategy,
//             maxRestarts,
//             restartWindow,
//             defaultRestartPolicy,
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
//         var selfRef = refFactory.Create("test-partition-supervisor", process);
//
//         supervisor.Context = new ActorContext(selfRef, sp.CreateScope());
//
//         return supervisor;
//     }
//
//     private static IActorFactory CreateFactory()
//     {
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(_ => new SimpleActor());
//         return factory;
//     }
//
//     private static Child CreateChild(
//         TestPartitionSupervisor supervisor,
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
//         TestPartitionSupervisor supervisor,
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
//     #region Initialization
//
//     [Test]
//     public async Task InitializeAsync_Should_CreateExactlyWorkersActors()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 3);
//
//         // Act
//         await supervisor.InitializeAsync();
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(3);
//         factory.Received(3).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task InitializeAsync_Should_CallOnInitializeAsync()
//     {
//         // Arrange
//         var called = false;
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 1,
//             onInitialize: _ =>
//             {
//                 called = true;
//                 return ValueTask.CompletedTask;
//             }
//         );
//
//         // Act
//         await supervisor.InitializeAsync();
//
//         // Assert
//         await Assert.That(called).IsTrue();
//     }
//
//     [Test]
//     public async Task InitializeAsync_Should_UseDefaultRestartPolicy()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             defaultRestartPolicy: RestartPolicy.Transient
//         );
//
//         // Act
//         await supervisor.InitializeAsync();
//
//         // Assert
//         await Assert.That(supervisor.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
//         await Assert.That(supervisor.Children[1].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
//     }
//
//     #endregion
//
//     #region Message Routing
//
//     [Test]
//     public async Task HandleAsync_Should_RouteActorProcessFailedMessage()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)
//                 new ActorProcessFailed(
//                     child.Process,
//                     new TellMessage("test", []),
//                     new Exception("fail")
//                 )
//         );
//
//         // Assert - factory called: 1 (init) + 1 (restart) = 2
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleAsync_Should_RouteActorProcessStoppedMessage()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act - permanent actor should be reset
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert - factory called: 1 (init) + 1 (reset) = 2
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleAsync_Should_RouteUnknownMessageToBase()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(workers: 1);
//
//         // Act & Assert - base HandleAsync throws for unhandled messages
//         var action = async () => await supervisor.HandleAsync((object)"unknown");
//         await Assert.That(action).ThrowsException();
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
//         var factory = Substitute.For<IActorFactory>();
//         factory.CreateActor(Arg.Any<Type>()).Returns(childActor);
//         var supervisor = CreateSupervisor(factory: factory, workers: 2);
//         var spec = new ChildSpecification(typeof(SimpleActor));
//
//         // Act
//         var child = supervisor.CreateActor(spec);
//         supervisor.Children = supervisor.Children.Add(child);
//         await supervisor.StartActorAsync(child);
//         await Task.Delay(50);
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(1);
//         await Assert.That(child.Actor).IsSameReferenceAs(childActor);
//         await Assert.That(childActor.Context).IsNotNull();
//         factory.Received(1).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task CreateActor_Should_PropagateRestartPolicy()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 2);
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
//     #endregion
//
//     #region Partition Routing
//
//     [Test]
//     public async Task GetActorReference_SameKey_Should_ReturnSameReference()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 3);
//         await supervisor.InitializeAsync();
//
//         // Act
//         var ref1 = supervisor.GetActorReference(42);
//         var ref2 = supervisor.GetActorReference(42);
//
//         // Assert
//         await Assert.That(ref1).IsSameReferenceAs(ref2);
//     }
//
//     [Test]
//     public async Task GetActorReference_DifferentKeys_Should_DistributeAcrossActors()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 3);
//         await supervisor.InitializeAsync();
//
//         // Act - try many keys
//         var refs = new HashSet<IActorReference>();
//         for (var i = 0; i < 1000; i++)
//         {
//             refs.Add(supervisor.GetActorReference(i));
//         }
//
//         // Assert - should hit more than one actor
//         await Assert.That(refs.Count).IsGreaterThan(1);
//     }
//
//     [Test]
//     public async Task GetHashcode_Should_UseHashCodeCombine()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(workers: 1);
//
//         // Act
//         var hash = supervisor.GetHashcode("test");
//         var expected = HashCode.Combine("test");
//
//         // Assert
//         await Assert.That(hash).IsEqualTo(expected);
//     }
//
//     #endregion
//
//     #region Actor Failure Handling
//
//     [Test]
//     public async Task HandleActorFailed_OneForOne_Should_RestartOnlyFailedActor()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.OneForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)
//                 new ActorProcessFailed(
//                     child.Process,
//                     new TellMessage("test", []),
//                     new Exception("fail")
//                 )
//         );
//
//         // Assert - factory: 2 (init) + 1 (restart) = 3
//         factory.Received(3).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_AllForOne_Should_RestartAllActors()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.AllForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)
//                 new ActorProcessFailed(
//                     child.Process,
//                     new TellMessage("test", []),
//                     new Exception("fail")
//                 )
//         );
//
//         // Assert - factory: 2 (init) + 2 (restart all) = 4
//         factory.Received(4).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorFailed_WithAskMessage_Should_CancelAskMessage()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//         var askMessage = new AskMessage("test", [], CancellationToken.None);
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessFailed(child.Process, askMessage, new Exception("fail"))
//         );
//
//         // Assert
//         var action = async () => await askMessage.AsTask();
//         await Assert.That(action).Throws<TaskCanceledException>();
//     }
//
//     [Test]
//     public async Task HandleActorFailed_WithEscalateException_Should_CancelNestedAskMessage()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//         var nestedAskMessage = new AskMessage("nested", [], CancellationToken.None);
//         var tellMessage = new TellMessage("test", []);
//         var escalateException = new EscalateFailureException(
//             "escalated",
//             child.Reference,
//             nestedAskMessage,
//             new Exception("inner")
//         );
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessFailed(child.Process, tellMessage, escalateException)
//         );
//
//         // Assert
//         var action = async () => await nestedAskMessage.AsTask();
//         await Assert.That(action).Throws<TaskCanceledException>();
//     }
//
//     [Test]
//     public async Task HandleActorFailed_UnknownActor_Should_NoOp()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var unknownProcess = Substitute.For<IActorProcess>();
//
//         // Act & Assert — should not throw
//         await supervisor.HandleAsync(
//             (object)
//                 new ActorProcessFailed(
//                     unknownProcess,
//                     new TellMessage("test", []),
//                     new Exception("fail")
//                 )
//         );
//     }
//
//     [Test]
//     public async Task HandleActorFailed_ExceedsMaxRestarts_Should_Escalate()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 1,
//             maxRestarts: 1,
//             restartWindow: TimeSpan.FromMinutes(10)
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // First failure - restart
//         await supervisor.HandleAsync(
//             (object)
//                 new ActorProcessFailed(
//                     child.Process,
//                     new TellMessage("test", []),
//                     new Exception("fail1")
//                 )
//         );
//
//         // Get the updated child after restart
//         var restartedChild = supervisor.Children[0];
//
//         // Second failure - should escalate
//         var action = async () =>
//             await supervisor.HandleAsync(
//                 (object)
//                     new ActorProcessFailed(
//                         restartedChild.Process,
//                         new TellMessage("test", []),
//                         new Exception("fail2")
//                     )
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
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert - factory: 1 (init) + 1 (reset) = 2
//         factory.Received(2).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task HandleActorStopped_NonPermanentActor_Should_TerminateReference()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 1,
//             defaultRestartPolicy: RestartPolicy.Transient
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//         var terminateCalled = false;
//         child.Reference.Terminated += (_, _) => terminateCalled = true;
//
//         // Act
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(child.Process, TerminatedReason.Stopped)
//         );
//
//         // Assert
//         await Assert.That(terminateCalled).IsTrue();
//     }
//
//     [Test]
//     public async Task HandleActorStopped_UnknownActor_Should_NoOp()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var unknownProcess = Substitute.For<IActorProcess>();
//
//         // Act & Assert — should not throw
//         await supervisor.HandleAsync(
//             (object)new ActorProcessStopped(unknownProcess, TerminatedReason.Stopped)
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
//     public async Task GetFailureAction_Should_ReturnRestart_BelowMaxRestarts()
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
//     public async Task GetFailureAction_Should_ReturnEscalate_AtMaxRestarts()
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
//     #endregion
//
//     #region Apply Actions
//
//     [Test]
//     public async Task StopAsync_OneForOne_Should_StopOnlyFailedActor()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.OneForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var failedChild = supervisor.Children[0];
//
//         // Act
//         await supervisor.StopAsync(failedChild);
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(2);
//     }
//
//     [Test]
//     public async Task StopAsync_AllForOne_Should_StopAllChildren()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.AllForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var failedChild = supervisor.Children[0];
//
//         // Act
//         await supervisor.StopAsync(failedChild);
//
//         // Assert
//         await Assert.That(supervisor.Children.Count).IsEqualTo(2);
//     }
//
//     [Test]
//     public async Task ResumeActorAsync_Should_RestartProcess()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act - should not throw
//         await supervisor.ResumeActorAsync(child);
//
//         // Assert
//         await Assert.That(child.Process).IsNotNull();
//     }
//
//     [Test]
//     public async Task EscalateAsync_Should_ThrowEscalateFailureException()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
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
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.OneForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//         var originalActor = child.Actor;
//
//         // Act
//         await supervisor.RestartAsync(child);
//
//         // Assert
//         await Assert.That(child.RestartCount).IsEqualTo(1);
//         await Assert.That(child.Actor).IsNotSameReferenceAs(originalActor);
//         // factory: 2 (init) + 1 (restart) = 3
//         factory.Received(3).CreateActor(typeof(SimpleActor));
//     }
//
//     [Test]
//     public async Task RestartAsync_AllForOne_Should_ResetAllChildren()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(
//             factory: factory,
//             workers: 2,
//             strategy: Strategy.AllForOne
//         );
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
//
//         // Act
//         await supervisor.RestartAsync(child);
//
//         // Assert - factory: 2 (init) + 2 (restart all) = 4
//         factory.Received(4).CreateActor(typeof(SimpleActor));
//         await Assert.That(child.RestartCount).IsEqualTo(1);
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
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
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
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
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
//     #region BeforeRestart Actor
//
//     [Test]
//     public async Task BeforeRestartActorAsync_Should_CallBeforeRestartOnActor()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         await supervisor.InitializeAsync();
//
//         var child = supervisor.Children[0];
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
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
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
//         var supervisor = CreateSupervisor(factory: factory, workers: 1);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-partition-supervisor-events", process);
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
//         // Assert
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
//         var supervisor = CreateSupervisor(workers: 1);
//         var supervisorMailbox = new ChannelMailbox();
//         var sp = supervisor.Context.ServiceProvider;
//         var process = new ActorProcess(supervisor, supervisorMailbox);
//         var refFactory = sp.GetRequiredService<IActorReferenceFactory>();
//         var selfRef = refFactory.Create("test-partition-supervisor-term", process);
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
//         var supervisor = CreateSupervisor(factory: factory, workers: 2);
//         await supervisor.InitializeAsync();
//
//         var actor1 = (DisposableActor)supervisor.Children[0].Actor;
//         var actor2 = (DisposableActor)supervisor.Children[1].Actor;
//
//         // Act
//         await supervisor.DisposeAsync();
//
//         // Assert
//         await Assert.That(actor1.AsyncDisposed).IsTrue();
//         await Assert.That(actor2.AsyncDisposed).IsTrue();
//         await Assert.That(supervisor.Children.Count).IsEqualTo(0);
//     }
//
//     [Test]
//     public async Task DisposeObjectAsync_Should_CallDisposeAsync_ForAsyncDisposable()
//     {
//         // Arrange
//         var supervisor = CreateSupervisor(workers: 1);
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
//         var supervisor = CreateSupervisor(workers: 1);
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
//         var supervisor = CreateSupervisor(workers: 1);
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
//         var supervisor = CreateSupervisor(factory: factory, workers: 2);
//         await supervisor.InitializeAsync();
//
//         var actor1 = (DisposableActor)supervisor.Children[0].Actor;
//         var actor2 = (DisposableActor)supervisor.Children[1].Actor;
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
//
//     #region ISupervisor.Children
//
//     [Test]
//     public async Task ISupervisorChildren_Should_ReturnActorReferences()
//     {
//         // Arrange
//         var factory = CreateFactory();
//         var supervisor = CreateSupervisor(factory: factory, workers: 2);
//         await supervisor.InitializeAsync();
//
//         // Act
//         var refs = ((ISupervisor)supervisor).Children.ToList();
//
//         // Assert
//         await Assert.That(refs.Count).IsEqualTo(2);
//         await Assert.That(refs[0]).IsSameReferenceAs(supervisor.Children[0].Reference);
//         await Assert.That(refs[1]).IsSameReferenceAs(supervisor.Children[1].Reference);
//     }
//
//     #endregion
// }
