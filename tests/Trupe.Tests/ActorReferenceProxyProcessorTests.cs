using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.SystemMessages;
using Trupe.Messages;
using Trupe.Pipelines;

namespace Trupe.Tests;

public class ActorReferenceProxyProcessorTests
{
    [Test]
    public async Task Ask_Should_BeExecutedWithSucess()
    {
        AskMessage? message = null;
        var pipelineContextFactory = Substitute.For<ISendPipelineContextFactory>();
        pipelineContextFactory
            .When(x =>
                x.Create(
                    Arg.Any<IActorReference>(),
                    Arg.Any<Type>(),
                    Arg.Any<IMessage>(),
                    Arg.Any<object?[]>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(x => message = (AskMessage)x[2]);

        var expecetedResponse = new SomeResponse();
        var pipeline = Substitute.For<ISendPipeline>();
        pipeline
            .When(async x => await x.ExecuteAsync(Arg.Any<ISendPipelineContext>()))
            .Do(x => message!.SetResult(expecetedResponse));

        var serviceProvider = CreateScope(pipeline, pipelineContextFactory);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        var response = @ref.Ask<SomeResponse>(new SomeRequest());

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
        await Assert.That(response).IsEqualTo(expecetedResponse);
    }

    [Test]
    public async Task Ask_Should_NotSilientThrow()
    {
        var pipeline = Substitute.For<ISendPipeline>();
        _ = pipeline.ExecuteAsync(Arg.Any<ISendPipelineContext>()).Throws(new Exception());

        var serviceProvider = CreateScope(pipeline);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await Assert.That(() => @ref.Ask<SomeResponse>(new SomeRequest())).Throws<Exception>();

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task AskAsync_Should_BeExecutedWithSucess()
    {
        AskMessage? message = null;
        var pipelineContextFactory = Substitute.For<ISendPipelineContextFactory>();
        pipelineContextFactory
            .When(x =>
                x.Create(
                    Arg.Any<IActorReference>(),
                    Arg.Any<Type>(),
                    Arg.Any<IMessage>(),
                    Arg.Any<object?[]>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(x => message = (AskMessage)x[2]);

        var expecetedResponse = new SomeResponse();
        var pipeline = Substitute.For<ISendPipeline>();
        pipeline
            .When(async x => await x.ExecuteAsync(Arg.Any<ISendPipelineContext>()))
            .Do(x => message!.SetResult(expecetedResponse));

        var serviceProvider = CreateScope(pipeline, pipelineContextFactory);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        var response = await @ref.AskAsync<SomeResponse>(new SomeRequest());

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
        await Assert.That(response).IsEqualTo(expecetedResponse);
    }

    [Test]
    public async Task AskAsync_Should_NotSilientThrow()
    {
        var pipeline = Substitute.For<ISendPipeline>();
        _ = pipeline.ExecuteAsync(Arg.Any<ISendPipelineContext>()).Throws(new Exception());

        var serviceProvider = CreateScope(pipeline);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await Assert
            .That(async () => await @ref.AskAsync<SomeResponse>(new SomeRequest()))
            .Throws<Exception>();

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task KillAsync_Should_CallProcessKill()
    {
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        var registry = Substitute.For<IActorProcessRegistry>();
        serviceProvider.GetService(typeof(IActorProcessRegistry)).Returns(registry);

        var process = Substitute.For<IActorProcess>();
        registry.Get(Arg.Any<IActorReference>()).Returns(process);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await @ref.KillAsync();

        await process.Received(1).KillAsync();
        _ = serviceProvider.Received(2).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task Stop_Should_UseTell()
    {
        var pipelineContextFactory = Substitute.For<ISendPipelineContextFactory>();

        var serviceProvider = CreateScope(pipelineContextFactory: pipelineContextFactory);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        @ref.Stop();

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
        _ = pipelineContextFactory
            .Received(1)
            .Create(
                Arg.Any<IActorReference>(),
                Arg.Any<Type>(),
                Arg.Is<IMessage>(m => m is TellMessage && m.Payload is Stop),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task Terminate_Should_RaiseEvent()
    {
        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            Substitute.For<IServiceProvider>()
        );

        var invoked = false;
        @ref.Terminated += (_, _) => invoked = true;

        @ref.MarkAsTerminate(TerminatedReason.Stopped);

        await Assert.That(invoked).IsTrue();
    }

    [Test]
    public void Tell_Should_BeExecutedWithSucess()
    {
        var serviceProvider = CreateScope();

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        @ref.Tell(new SomeRequest());

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task Tell_Should_NotSilientThrow()
    {
        var pipeline = Substitute.For<ISendPipeline>();

        _ = pipeline.ExecuteAsync(Arg.Any<ISendPipelineContext>()).Throws(new Exception());

        var serviceProvider = CreateScope(pipeline);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await Assert.That(() => @ref.Tell(new SomeRequest())).Throws<Exception>();

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task TellAsync_Should_BeExecutedWithSucess()
    {
        var serviceProvider = CreateScope();

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await @ref.TellAsync(new SomeRequest());

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    [Test]
    public async Task TellAsync_Should_NotSilientThrow()
    {
        var pipeline = Substitute.For<ISendPipeline>();

        _ = pipeline.ExecuteAsync(Arg.Any<ISendPipelineContext>()).Throws(new Exception());

        var serviceProvider = CreateScope(pipeline);

        var @ref = new ActorReferenceProxyProcessor(
            new Uri("trup://localhost/123"),
            typeof(SomeActor),
            serviceProvider
        );

        await Assert.That(async () => await @ref.TellAsync(new SomeRequest())).Throws<Exception>();

        _ = serviceProvider.Received(5).GetService(Arg.Any<Type>());
    }

    private static IServiceProvider CreateScope(
        ISendPipeline? pipeline = null,
        ISendPipelineContextFactory? pipelineContextFactory = null
    )
    {
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        var registry = Substitute.For<IActorProcessRegistry>();
        serviceProvider.GetService(typeof(IActorProcessRegistry)).Returns(registry);

        var pipelineFactory = Substitute.For<ISendPipelineFactory>();
        serviceProvider.GetService(typeof(ISendPipelineFactory)).Returns(pipelineFactory);

        pipelineContextFactory ??= Substitute.For<ISendPipelineContextFactory>();
        serviceProvider
            .GetService(typeof(ISendPipelineContextFactory))
            .Returns(pipelineContextFactory);

        pipeline ??= Substitute.For<ISendPipeline>();
        pipelineFactory.Create(Arg.Any<Type>(), Arg.Any<Type>()).Returns(pipeline);

        var context = Substitute.For<ISendPipelineContext>();
        pipelineContextFactory
            .Create(
                Arg.Any<IActorReference>(),
                Arg.Any<Type>(),
                Arg.Any<IMessage>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(context);

        var accessor = new SettableSendPipelineContextAccessor();
        serviceProvider.GetService(typeof(SettableSendPipelineContextAccessor)).Returns(accessor);

        return serviceProvider;
    }

    public class SomeActor : Actor { }

    public record SomeRequest();

    public record SomeResponse();
}
