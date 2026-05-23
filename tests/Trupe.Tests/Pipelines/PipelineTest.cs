// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Options;
// using NSubstitute;
// using Trupe.Abstractions;
// using Trupe.Abstractions.Exceptions;
// using Trupe.Abstractions.Mailboxes;
// using Trupe.Abstractions.Messages;
// using Trupe.Abstractions.Options;
// using Trupe.Abstractions.Pipelines;
// using Trupe.Abstractions.SystemMessages;
// using Trupe.Extensions;
// using Trupe.Messages;
// using Trupe.Pipelines;
// using Trupe.Pipelines.Middlewares;
//
// namespace Trupe.Tests.Pipelines;
//
// public class PipelineTest
// {
//     #region SendPipeline
//
//     [Test]
//     public async Task SendPipeline_EmptyMiddlewares_ShouldReturnImmediately()
//     {
//         // Arrange
//         var pipeline = new SendPipeline(ImmutableList<ISendMiddleware>.Empty);
//         var context = Substitute.For<ISendPipelineContext>();
//
//         // Act & Assert - should not throw
//         await pipeline.ExecuteAsync(context);
//     }
//
//     [Test]
//     public async Task SendPipeline_ShouldExecuteMiddlewaresInOrder()
//     {
//         // Arrange
//         var executionOrder = new List<int>();
//
//         var middleware1 = new OrderTrackingSendMiddleware(executionOrder, 1);
//         var middleware2 = new OrderTrackingSendMiddleware(executionOrder, 2);
//
//         var pipeline = new SendPipeline([middleware1, middleware2]);
//         var context = Substitute.For<ISendPipelineContext>();
//
//         // Act
//         await pipeline.ExecuteAsync(context);
//
//         // Assert
//         await Assert.That(executionOrder).IsEquivalentTo(new[] { 1, 2 });
//     }
//
//     [Test]
//     public async Task SendPipeline_MiddlewareCanShortCircuit()
//     {
//         // Arrange
//         var middleware1 = Substitute.For<ISendMiddleware>();
//         middleware1
//             .InvokeAsync(Arg.Any<ISendPipelineContext>(), Arg.Any<NextSendDelegate>())
//             .Returns(ValueTask.CompletedTask); // does NOT call next
//
//         var middleware2 = Substitute.For<ISendMiddleware>();
//
//         var pipeline = new SendPipeline([middleware1, middleware2]);
//         var context = Substitute.For<ISendPipelineContext>();
//
//         // Act
//         await pipeline.ExecuteAsync(context);
//
//         // Assert
//         await middleware2
//             .DidNotReceive()
//             .InvokeAsync(Arg.Any<ISendPipelineContext>(), Arg.Any<NextSendDelegate>());
//     }
//
//     #endregion
//
//     #region ReceivePipeline
//
//     [Test]
//     public async Task ReceivePipeline_EmptyMiddlewares_ShouldReturnImmediately()
//     {
//         // Arrange
//         var pipeline = new ReceivePipeline(ImmutableList<IReceiveMiddleware>.Empty);
//         var context = Substitute.For<IReceivePipelineContext>();
//
//         // Act & Assert
//         await pipeline.ExecuteAsync(context);
//     }
//
//     [Test]
//     public async Task ReceivePipeline_ShouldExecuteMiddlewaresInOrder()
//     {
//         // Arrange
//         var executionOrder = new List<int>();
//
//         var middleware1 = new OrderTrackingReceiveMiddleware(executionOrder, 1);
//         var middleware2 = new OrderTrackingReceiveMiddleware(executionOrder, 2);
//
//         var pipeline = new ReceivePipeline([middleware1, middleware2]);
//         var context = Substitute.For<IReceivePipelineContext>();
//
//         // Act
//         await pipeline.ExecuteAsync(context);
//
//         // Assert
//         await Assert.That(executionOrder).IsEquivalentTo(new[] { 1, 2 });
//     }
//
//     [Test]
//     public async Task ReceivePipeline_MiddlewareCanShortCircuit()
//     {
//         // Arrange
//         var middleware1 = Substitute.For<IReceiveMiddleware>();
//         middleware1
//             .InvokeAsync(Arg.Any<IReceivePipelineContext>(), Arg.Any<NextReceiveDelegate>())
//             .Returns(ValueTask.CompletedTask);
//
//         var middleware2 = Substitute.For<IReceiveMiddleware>();
//
//         var pipeline = new ReceivePipeline([middleware1, middleware2]);
//         var context = Substitute.For<IReceivePipelineContext>();
//
//         // Act
//         await pipeline.ExecuteAsync(context);
//
//         // Assert
//         await middleware2
//             .DidNotReceive()
//             .InvokeAsync(Arg.Any<IReceivePipelineContext>(), Arg.Any<NextReceiveDelegate>());
//     }
//
//     #endregion
//
//     #region PipelineRegistry
//
//     [Test]
//     public async Task PipelineRegistry_ShouldReturnMiddlewaresMatchingActorAndMessageType()
//     {
//         // Arrange
//         var options = Options.Create(
//             new PipelineOptions
//             {
//                 Middlewares =
//                 [
//                     new PipelineMiddlewareConfiguration
//                     {
//                         Order = 1,
//                         MiddlewareType = typeof(ActorProcessDispatcherMiddleware),
//                         ActorType = null,
//                         MessageType = null,
//                     },
//                 ],
//             }
//         );
//
//         var registry = new PipelineRegistry(options);
//
//         // Act
//         var middlewares = registry.GetMiddlewares(typeof(Actor), typeof(string));
//
//         // Assert
//         await Assert.That(middlewares).IsNotNull();
//         var list = new List<IMiddlewareConfiguration>(middlewares);
//         await Assert.That(list.Count).IsGreaterThanOrEqualTo(1);
//     }
//
//     [Test]
//     public async Task PipelineRegistry_ShouldFilterByActorType()
//     {
//         // Arrange
//         var options = Options.Create(
//             new PipelineOptions
//             {
//                 Middlewares =
//                 [
//                     new PipelineMiddlewareConfiguration
//                     {
//                         Order = 1,
//                         MiddlewareType = typeof(ActorProcessDispatcherMiddleware),
//                         ActorType = typeof(TestActor),
//                         MessageType = null,
//                     },
//                 ],
//             }
//         );
//
//         var registry = new PipelineRegistry(options);
//
//         // Act - Actor type does not match
//         var middlewares = registry.GetMiddlewares(typeof(Actor), typeof(string));
//
//         // Assert
//         var list = new List<IMiddlewareConfiguration>(middlewares);
//         await Assert.That(list.Count).IsEqualTo(0);
//     }
//
//     [Test]
//     public async Task PipelineRegistry_ShouldFilterByMessageType()
//     {
//         // Arrange
//         var options = Options.Create(
//             new PipelineOptions
//             {
//                 Middlewares =
//                 [
//                     new PipelineMiddlewareConfiguration
//                     {
//                         Order = 1,
//                         MiddlewareType = typeof(ActorProcessDispatcherMiddleware),
//                         ActorType = null,
//                         MessageType = typeof(int),
//                     },
//                 ],
//             }
//         );
//
//         var registry = new PipelineRegistry(options);
//
//         // Act - message type does not match
//         var middlewares = registry.GetMiddlewares(typeof(Actor), typeof(string));
//         var list = new List<IMiddlewareConfiguration>(middlewares);
//         await Assert.That(list.Count).IsEqualTo(0);
//
//         // Act - message type matches
//         var middlewares2 = registry.GetMiddlewares(typeof(Actor), typeof(int));
//         var list2 = new List<IMiddlewareConfiguration>(middlewares2);
//         await Assert.That(list2.Count).IsEqualTo(1);
//     }
//
//     [Test]
//     public async Task PipelineRegistry_ShouldSetCorrectScope()
//     {
//         // Arrange
//         var options = Options.Create(
//             new PipelineOptions
//             {
//                 Middlewares =
//                 [
//                     new PipelineMiddlewareConfiguration
//                     {
//                         Order = 1,
//                         MiddlewareType = typeof(ActorProcessDispatcherMiddleware), // ISendMiddleware only
//                     },
//                     new PipelineMiddlewareConfiguration
//                     {
//                         Order = 2,
//                         MiddlewareType = typeof(AskMiddleware), // IReceiveMiddleware only
//                     },
//                 ],
//             }
//         );
//
//         var registry = new PipelineRegistry(options);
//         var middlewares = registry.GetMiddlewares(typeof(Actor), typeof(string));
//         var list = new List<IMiddlewareConfiguration>(middlewares);
//
//         // Assert
//         await Assert.That(list.Count).IsEqualTo(2);
//         await Assert.That(list[0].Scope).IsEqualTo(MiddlewareScope.Send);
//         await Assert.That(list[1].Scope).IsEqualTo(MiddlewareScope.Receive);
//     }
//
//     #endregion
//
//     #region MailboxDispatcherMiddleware
//
//     [Test]
//     public async Task MailboxDispatcherMiddleware_ShouldEnqueueMessageToMailbox()
//     {
//         // Arrange
//         var mailbox = Substitute.For<IMailbox>();
//         mailbox
//             .EnqueueAsync(Arg.Any<IMessage>(), Arg.Any<CancellationToken>())
//             .Returns(ValueTask.CompletedTask);
//
//         var process = Substitute.For<IActorProcess>();
//         process.Mailbox.Returns(mailbox);
//
//         var message = new TellMessage("hello", []);
//         var metadata = new PipelineMetadataCollection(
//             [new Trupe.Abstractions.Pipelines.Metadatas.ActorProcessMetadata(process)]
//         );
//
//         var context = Substitute.For<ISendPipelineContext>();
//         context.Message.Returns(message);
//         context.Metadata.Returns(metadata);
//         context.CancellationToken.Returns(CancellationToken.None);
//
//         var middleware = new ActorProcessDispatcherMiddleware();
//         NextSendDelegate next = _ => ValueTask.CompletedTask;
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         await mailbox.Received(1).EnqueueAsync(message, CancellationToken.None);
//     }
//
//     #endregion
//
//     #region ActorMessageDispatcherMiddleware
//
//     [Test]
//     public async Task ActorMessageDispatcher_ShouldCallInitializeAsync_ForInitializeActorMessage()
//     {
//         // Arrange
//         var actor = Substitute.For<IActor>();
//         actor.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
//
//         var message = Substitute.For<IMessage>();
//         message.Payload.Returns(new InitializeActor());
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Actor.Returns(actor);
//         context.Message.Returns(message);
//         context.CancellationToken.Returns(CancellationToken.None);
//
//         var middleware = new ActorMessageDispatcherMiddleware();
//         var nextCalled = false;
//         NextReceiveDelegate next = _ =>
//         {
//             nextCalled = true;
//             return ValueTask.CompletedTask;
//         };
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         await actor.Received(1).InitializeAsync(CancellationToken.None);
//         await Assert.That(nextCalled).IsTrue();
//     }
//
//     [Test]
//     public async Task ActorMessageDispatcher_ShouldCallAfterRestartAsync_ForAfterRestartMessage()
//     {
//         // Arrange
//         var actor = Substitute.For<IActor>();
//         actor.AfterRestartAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
//
//         var message = Substitute.For<IMessage>();
//         message.Payload.Returns(new AfterRestartActor());
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Actor.Returns(actor);
//         context.Message.Returns(message);
//         context.CancellationToken.Returns(CancellationToken.None);
//
//         var middleware = new ActorMessageDispatcherMiddleware();
//         NextReceiveDelegate next = _ => ValueTask.CompletedTask;
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         await actor.Received(1).AfterRestartAsync(CancellationToken.None);
//     }
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task ActorMessageDispatcher_ShouldCallHandleAsync_ForRegularMessage()
//     {
//         // Arrange
//         var actor = Substitute.For<IActor>();
//         actor
//             .HandleAsync(Arg.Any<object?>(), Arg.Any<CancellationToken>())
//             .Returns(ValueTask.CompletedTask);
//
//         var payload = "test-message";
//         var message = Substitute.For<IMessage>();
//         message.Payload.Returns(payload);
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Actor.Returns(actor);
//         context.Message.Returns(message);
//         context.CancellationToken.Returns(CancellationToken.None);
//
//         var middleware = new ActorMessageDispatcherMiddleware();
//         NextReceiveDelegate next = _ => ValueTask.CompletedTask;
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         await actor.Received(1).HandleAsync(payload, CancellationToken.None);
//     }
//
//     #endregion
//
//     #region AskMiddleware
//
//     [Test]
//     public async Task AskMiddleware_ShouldSetResult_OnAskMessage()
//     {
//         // Arrange
//         var askMessage = new AskMessage("request", []);
//         var expectedResponse = "response-value";
//
//         var actorContext = Substitute.For<IActorContext>();
//         actorContext.Response.Returns(expectedResponse);
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Message.Returns(askMessage);
//         context.ActorContext.Returns(actorContext);
//
//         var middleware = new AskMiddleware();
//         NextReceiveDelegate next = _ => ValueTask.CompletedTask;
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         var result = await askMessage.AsTask();
//         await Assert.That(result).IsEqualTo(expectedResponse);
//     }
//
//     [Test]
//     public async Task AskMiddleware_ShouldSetException_OnAskException()
//     {
//         // Arrange
//         var askMessage = new AskMessage("request", []);
//         var expectedException = new TestAskException("something failed");
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Message.Returns(askMessage);
//
//         var middleware = new AskMiddleware();
//         NextReceiveDelegate next = _ => throw expectedException;
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert - the task should be faulted with the exception
//         var task = askMessage.AsTask();
//         await Assert.That(task.IsFaulted).IsTrue();
//     }
//
//     [Test]
//     public async Task AskMiddleware_ShouldCallNext_ForNonAskMessage()
//     {
//         // Arrange
//         var tellMessage = new TellMessage("hello", []);
//
//         var context = Substitute.For<IReceivePipelineContext>();
//         context.Message.Returns(tellMessage);
//
//         var middleware = new AskMiddleware();
//         var nextCalled = false;
//         NextReceiveDelegate next = _ =>
//         {
//             nextCalled = true;
//             return ValueTask.CompletedTask;
//         };
//
//         // Act
//         await middleware.InvokeAsync(context, next);
//
//         // Assert
//         await Assert.That(nextCalled).IsTrue();
//     }
//
//     #endregion
//
//     #region SendPipelineFactory
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task SendPipelineFactory_ShouldCreatePipelineWithRegisteredMiddlewares()
//     {
//         // Arrange
//         var services = new ServiceCollection();
//         services.AddTrupe(c => { });
//         var sp = services.BuildServiceProvider();
//
//         var factory = sp.GetRequiredService<ISendPipelineFactory>();
//
//         // Act
//         var pipeline = factory.Create(typeof(TestActor), typeof(string));
//
//         // Assert
//         await Assert.That(pipeline).IsNotNull();
//         await Assert.That(pipeline).IsTypeOf<SendPipeline>();
//     }
//
//     #endregion
//
//     #region ReceivePipelineFactory
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task ReceivePipelineFactory_ShouldCreatePipelineWithRegisteredMiddlewares()
//     {
//         // Arrange
//         var services = new ServiceCollection();
//         services.AddTrupe(c => { });
//         var sp = services.BuildServiceProvider();
//
//         var factory = sp.GetRequiredService<IReceivePipelineFactory>();
//
//         // Act
//         var pipeline = factory.Create(typeof(TestActor), typeof(string));
//
//         // Assert
//         await Assert.That(pipeline).IsNotNull();
//         await Assert.That(pipeline).IsTypeOf<ReceivePipeline>();
//     }
//
//     #endregion
//
//     #region Helpers
//
//     private class TestActor : Actor
//     {
//         public override ValueTask HandleAsync(
//             object? message,
//             CancellationToken cancellationToken = default
//         )
//         {
//             return ValueTask.CompletedTask;
//         }
//     }
//
//     private class TestAskException(string? message) : AskException(message);
//
//     private class OrderTrackingSendMiddleware(List<int> order, int id) : ISendMiddleware
//     {
//         public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
//         {
//             order.Add(id);
//             await next(context);
//         }
//     }
//
//     private class OrderTrackingReceiveMiddleware(List<int> order, int id) : IReceiveMiddleware
//     {
//         public async ValueTask InvokeAsync(
//             IReceivePipelineContext context,
//             NextReceiveDelegate next
//         )
//         {
//             order.Add(id);
//             await next(context);
//         }
//     }
//
//     #endregion
// }
