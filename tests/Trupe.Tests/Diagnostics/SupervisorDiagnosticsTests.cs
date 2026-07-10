using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;
using Trupe.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Tests.Diagnostics;

public class SupervisorDiagnosticsTests
{
    [Test]
    public async Task HandleAsync_AddActor_Should_IncrementChildAddedCounter()
    {
        using var collector = new MetricsCollector();
        var supervisor = new TestSupervisor(Substitute.For<ILogger<TestSupervisor>>())
        {
            Context = CreateContext(),
        };

        var child = CreateChild<ActorA>();
        await supervisor.HandleAsync(new AddActor(child), CancellationToken.None);

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "supervisor.child.added");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task HandleAsync_AddActor_Should_Tag_Actor()
    {
        using var collector = new MetricsCollector();
        var supervisor = new TestSupervisor(Substitute.For<ILogger<TestSupervisor>>())
        {
            Context = CreateContext(),
        };

        var child = CreateChild<ActorA>();
        await supervisor.HandleAsync(new AddActor(child), CancellationToken.None);

        var measurement = collector.Measurements.First(m => m.Name == "supervisor.child.added");
        var actorTag = measurement.Tags.FirstOrDefault(t => t.Key == "actor");
        await Assert.That(actorTag.Value).IsEqualTo(child.Name);
    }

    [Test]
    public async Task HandleAsync_AddActor_Should_UpdateChildrenActiveGauge()
    {
        using var collector = new MetricsCollector();
        var supervisor = new TestSupervisor(Substitute.For<ILogger<TestSupervisor>>())
        {
            Context = CreateContext(),
        };

        var child = CreateChild<ActorA>();
        await supervisor.HandleAsync(new AddActor(child), CancellationToken.None);

        var measurement = collector.Measurements.LastOrDefault(m => m.Name == "supervisor.children.active");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task HandleAsync_AddActor_Twice_Should_ReflectBothInActiveGauge()
    {
        using var collector = new MetricsCollector();
        var supervisor = new TestSupervisor(Substitute.For<ILogger<TestSupervisor>>())
        {
            Context = CreateContext(),
        };

        await supervisor.HandleAsync(new AddActor(CreateChild<ActorA>()), CancellationToken.None);
        await supervisor.HandleAsync(new AddActor(CreateChild<ActorA>()), CancellationToken.None);

        var measurement = collector.Measurements.LastOrDefault(m => m.Name == "supervisor.children.active");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(2);
    }

    private static Child CreateChild<T>()
        where T : IActor, new()
    {
        var actor = new T { Context = Substitute.For<IActorContext>() };
        actor.Context.Name.Returns(new Uri($"trupe://localhost/{Guid.NewGuid()}"));
        actor.Context.Self.Returns(Substitute.For<IActorReference>());
        var process = Substitute.For<IActorProcess>();
        return new Child(
            actor,
            process,
            RestartPolicy.Transient,
            _ => Substitute.For<IMailbox>(),
            typeof(T)
        );
    }

    private static IActorContext CreateContext()
    {
        var context = Substitute.For<IActorContext>();
        context.Name.Returns(new Uri("trupe://localhost/supervisor-diagnostics-test"));
        context.Metadata.Returns([]);

        var reference = Substitute.For<IActorReference>();
        context.Self.Returns(reference);

        var serviceProvider = Substitute.For<IServiceProvider>();
        context.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var scope = Substitute.For<IServiceScope>();
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        var actorFactory = Substitute.For<IActorFactory>();
        actorFactory.CreateActor(typeof(ActorA)).Returns(_ => new ActorA());
        serviceProvider.GetService(typeof(IActorFactory)).Returns(actorFactory);

        var registry = Substitute.For<IActorProcessRegistry>();
        serviceProvider.GetService(typeof(IActorProcessRegistry)).Returns(registry);

        var referenceFactory = Substitute.For<IActorReferenceFactory>();
        referenceFactory
            .Create(Arg.Any<string>(), Arg.Any<IActorProcess>())
            .Returns(_ => Substitute.For<IActorReference>());
        serviceProvider.GetService(typeof(IActorReferenceFactory)).Returns(referenceFactory);

        return context;
    }

    public class TestSupervisor(ILogger<TestSupervisor> logger) : Supervisor(logger)
    {
        public ImmutableList<Child> GetChildren() => Children;

        protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    public class ActorA : Actor;
}
