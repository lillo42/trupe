using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;
using Trupe.Extensions;

namespace Trupe.IntegrationTests;

public class ActorTest
{
    private IServiceProvider _serviceProvider = null!;
    private IRootSupervisor _rootSupervisor = null!;

    [Before(Test)]
    public async Task Before()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IActorProcessRegistry>(new ActorProcessRegistry());
        collection
            .AddLogging()
            .AddTrupe(opt =>
            {
                opt.AddActor<CustomActor>();
                opt.ConfigureRootSupervisor(root => root.AddActor<CustomActor>());
            });

        _serviceProvider = collection.BuildServiceProvider();
        _rootSupervisor = _serviceProvider.GetRequiredService<IRootSupervisor>();

        var system = _serviceProvider.GetRequiredService<ActorSystem>();
        await system.StartAsync();

        await Task.Delay(1_000);
    }

    [After(Test)]
    public async Task After()
    {
        var system = _serviceProvider.GetRequiredService<ActorSystem>();
        await system.StopAsync();
    }

    [Test]
    public async Task Ask_Should_PropagateAskException()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();

        await Assert.That(() => @ref.Ask<object>(new RaiseFailure()))
            .Throws<CustomAskException>();
    }
    
    [Test]
    public async Task AskAsync_Should_PropagateAskException()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();

        await Assert.That(async () => await @ref.AskAsync<object>(new RaiseFailure()))
            .Throws<CustomAskException>();
    }
    
    [Test]
    public async Task Ask_Should_BeCancelable()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();
        
        await Assert.That(() => @ref.Ask<object>(new Delay(TimeSpan.FromSeconds(2)), timeout: TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }
    
    [Test]
    public async Task AskAsync_Should_BeCancelable()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));
        
        await Assert.That(async () => await @ref.AskAsync<object>(new Delay(TimeSpan.FromSeconds(2)), cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    public record Echo(string Message);

    public record Ping(string Message);

    public record Pong(string Message);
    
    public record Delay(TimeSpan Duration);

    public record RaiseFailure;

    public class CustomAskException : AskException;

    public class CustomActor : Actor, IHandleActorMessage<Echo>,
        IHandleActorMessage<Ping>,
        IHandleActorMessage<RaiseFailure>,
        IHandleActorMessage<Delay>
    {
        public ValueTask HandleAsync(Echo message, CancellationToken cancellationToken = default)
        {
            return new ValueTask();
        }

        public ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
        {
            Context.Response = new Pong(message.Message);
            return new ValueTask();
        }

        public ValueTask HandleAsync(RaiseFailure message, CancellationToken cancellationToken = default)
        {
            throw new CustomAskException();
        }

        public async ValueTask HandleAsync(Delay message, CancellationToken cancellationToken = default)
        {
            await Task.Delay(message.Duration, cancellationToken);
            Context.Response = new object(); 
        }
    }
}