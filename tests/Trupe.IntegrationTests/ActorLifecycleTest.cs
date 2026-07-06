using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Extensions;

namespace Trupe.IntegrationTests;

public class ActorLifecycleTest
{
    private IServiceProvider _serviceProvider = null!;
    private IRootSupervisor _rootSupervisor = null!;
    private IActorProcessRegistry _registry = null!;

    [Before(Test)]
    public async Task Before()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IActorProcessRegistry>(new ActorProcessRegistry());
        collection
            .AddLogging()
            .AddTrupe(opt =>
            {
                opt.AddActor<LifecycleActor>();
                opt.ConfigureRootSupervisor(root => root.AddActor<LifecycleActor>());
            });

        _serviceProvider = collection.BuildServiceProvider();
        _rootSupervisor = _serviceProvider.GetRequiredService<IRootSupervisor>();
        _registry = _serviceProvider.GetRequiredService<IActorProcessRegistry>();

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
    public async Task InitializeAsync_Should_BeCalled()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();
        var process = _registry.GetProcess(@ref);

        var actor = (LifecycleActor)process.Actor;

        await Assert.That(actor.Initialized).IsTrue();
        await Assert.That(actor.BeforeRestart).IsFalse();
        await Assert.That(actor.AfterRestart).IsFalse();
    }

    [Test]
    public async Task BeforeAndAfter_Should_BeCalled_When_RaiseThrow()
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        var @ref = _rootSupervisor.Children.First();
        var process = _registry.GetProcess(@ref);
        var oldActor = (LifecycleActor)process.Actor;
        await @ref.TellAsync(new ForceRestart());

        await Task.Delay(1_000);

        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);

        @ref = _rootSupervisor.Children.First();
        process = _registry.GetProcess(@ref);
        var newActor = (LifecycleActor)process.Actor;

        await Assert.That(oldActor).IsNotEqualTo(newActor);
        await Assert.That(oldActor.BeforeRestart).IsTrue();
        await Assert.That(newActor.AfterRestart).IsTrue();
    }

    public record ForceRestart;

    public class LifecycleActor : Actor, IHandleActorMessage<ForceRestart>
    {
        public bool Initialized { get; private set; }
        public bool BeforeRestart { get; private set; }
        public bool AfterRestart { get; private set; }

        public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return base.InitializeAsync(cancellationToken);
        }

        public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
        {
            BeforeRestart = true;
            return base.BeforeRestartAsync(cancellationToken);
        }

        public override ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
        {
            AfterRestart = true;
            return base.AfterRestartAsync(cancellationToken);
        }

        public ValueTask HandleAsync(
            ForceRestart message,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException();
        }
    }
}
