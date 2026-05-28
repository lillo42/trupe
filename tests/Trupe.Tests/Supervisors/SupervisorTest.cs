using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;
using Trupe.Supervisors;

namespace Trupe.Tests.Supervisors;

public class SupervisorTest
{
    [Test]
    public async Task AddChildAsync_Should_Throw_When_SupervisorIsAlreadyInitialize()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await supervisor.InitializeAsync();

        await Assert
            .That(async () => await supervisor.TryAddAsync<ActorA>())
            .Throws<SupervisorAlreadyInitializedException>();
    }

    [Test]
    public async Task AddChild_Should_Throw_When_SupervisorIsAlreadyInitialize()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await supervisor.InitializeAsync();

        await Assert
            .That(supervisor.TryAdd<ActorA>)
            .Throws<SupervisorAlreadyInitializedException>();
    }

    private static IActorContext CreateContext(
        IActorFactory? actorFactory = null,
        IActorReference? reference = null,
        IActorReferenceFactory? referenceFactory = null,
        IActorProcessRegistry? registry = null,
        IServiceProvider? serviceProvider = null
    )
    {
        var context = Substitute.For<IActorContext>();

        var name = new Uri("trupr://localhost/123");
        context.Name.Returns(name);

        context.Metadata.Returns([]);

        reference ??= Substitute.For<IActorReference>();
        context.Self.Returns(reference);

        serviceProvider ??= Substitute.For<IServiceProvider>();
        context.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var scope = Substitute.For<IServiceScope>();
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        actorFactory ??= Substitute.For<IActorFactory>();
        actorFactory.CreateActor(typeof(ActorA)).Returns(_ => new ActorA());
        actorFactory.CreateActor(typeof(ActorB)).Returns(_ => new ActorB());
        serviceProvider.GetService(typeof(IActorFactory)).Returns(actorFactory);

        registry ??= Substitute.For<IActorProcessRegistry>();
        serviceProvider.GetService(typeof(IActorProcessRegistry)).Returns(registry);

        referenceFactory ??= Substitute.For<IActorReferenceFactory>();
        referenceFactory
            .Create(Arg.Any<string>(), Arg.Any<IActorProcess>())
            .Returns(_ => Substitute.For<IActorReference>());
        serviceProvider.GetService(typeof(IActorReferenceFactory)).Returns(referenceFactory);

        return context;
    }

    public class SupervisorA(ILogger<SupervisorA> logger) : Supervisor(logger)
    {
        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            await AddChildAsync<ActorA>(cancellationToken);
            await AddChildAsync<ActorB>(cancellationToken);
        }

        public async ValueTask TryAddAsync<T>()
            where T : IActor
        {
            await AddChildAsync<ActorA>();
        }

        public void TryAdd<T>()
            where T : IActor
        {
            AddChild<ActorA>();
        }
    }

    public record MessageA();

    public class ActorA : Actor, IHandleActorMessage<MessageA>
    {
        public ValueTask HandleAsync(
            MessageA message,
            CancellationToken cancellationToken = default
        )
        {
            throw new System.NotImplementedException();
        }
    }

    public class ActorB : Actor, IHandleActorMessage<MessageA>
    {
        public ValueTask HandleAsync(
            MessageA message,
            CancellationToken cancellationToken = default
        )
        {
            throw new System.NotImplementedException();
        }
    }
}
