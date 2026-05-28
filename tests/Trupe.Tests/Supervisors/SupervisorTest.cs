using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;
using Trupe.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Tests.Supervisors;

public class SupervisorTest
{
    [Test]
    public async Task AddChildAsync_Should_Throw_When_SupervisorIsAlreadyInitialized()
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
    public async Task AddChild_Should_Throw_When_SupervisorIsAlreadyInitialized()
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

    [Test]
    public async Task InitializeAsync_Should_SetInitializedToTrue()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await Assert.That(supervisor.IsInitialized).IsFalse();

        await supervisor.InitializeAsync();

        await Assert.That(supervisor.IsInitialized).IsTrue();
    }

    [Test]
    public async Task InitializeAsync_Should_AddChildrenDuringInitialization()
    {
        var reference = Substitute.For<IActorReference>();
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(reference: reference),
        };

        await supervisor.InitializeAsync();

        // OnInitializeAsync adds ActorA and ActorB via AddChildAsync, each calls TellAsync on Self
        await reference.Received(2).TellAsync(Arg.Any<AddActor>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_AddActor_Should_AddChildToChildrenList()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        var child = CreateChild<ActorA>();

        await supervisor.HandleAsync(new AddActor(child), CancellationToken.None);

        await Assert.That(supervisor.GetChildren()).Contains(child);
    }

    [Test]
    public async Task HandleAsync_WithAddActorMessage_Should_RouteToAddActorHandler()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        var child = CreateChild<ActorA>();
        object message = new AddActor(child);

        await supervisor.HandleAsync(message, CancellationToken.None);

        await Assert.That(supervisor.GetChildren()).Contains(child);
    }

    [Test]
    public async Task HandleAsync_WithNonAddActorMessage_Should_DelegateToBase()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        // Non-AddActor messages should be handled by base class (which throws UnhandleMessageException for unknown messages)
        await Assert
            .That(async () => await supervisor.HandleAsync("unknown", CancellationToken.None))
            .Throws<UnhandleMessageException>();
    }

    [Test]
    public async Task AddChildAsync_WithType_Should_Throw_When_AlreadyInitialized()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await supervisor.InitializeAsync();

        await Assert
            .That(async () => await supervisor.TryAddByTypeAsync(typeof(ActorA)))
            .Throws<SupervisorAlreadyInitializedException>();
    }

    [Test]
    public async Task AddChild_WithType_Should_Throw_When_AlreadyInitialized()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await supervisor.InitializeAsync();

        await Assert
            .That(() => supervisor.TryAddByType(typeof(ActorA)))
            .Throws<SupervisorAlreadyInitializedException>();
    }

    [Test]
    public async Task AddChild_BeforeInitialization_Should_NotThrow()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        // Should not throw before initialization
        await Assert.That(supervisor.TryAdd<ActorA>).ThrowsNothing();
    }

    [Test]
    public async Task AddChildAsync_BeforeInitialization_Should_NotThrow()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        await Assert.That(async () => await supervisor.TryAddAsync<ActorA>()).ThrowsNothing();
    }

    [Test]
    public async Task AddChild_Should_CreateActorViaFactory()
    {
        var actorFactory = Substitute.For<IActorFactory>();
        actorFactory.CreateActor(typeof(ActorA)).Returns(_ => new ActorA());
        actorFactory.CreateActor(typeof(ActorB)).Returns(_ => new ActorB());

        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(actorFactory: actorFactory),
        };

        supervisor.TryAdd<ActorA>();

        actorFactory.Received(1).CreateActor(typeof(ActorA));
    }

    [Test]
    public async Task AddChild_Should_TellSelfWithAddActorCommand()
    {
        var reference = Substitute.For<IActorReference>();
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(reference: reference),
        };

        supervisor.TryAdd<ActorA>();

        reference.Received(1).Tell(Arg.Any<AddActor>());
    }

    [Test]
    public async Task AddChildAsync_Should_TellAsyncSelfWithAddActorCommand()
    {
        var reference = Substitute.For<IActorReference>();
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(reference: reference),
        };

        await supervisor.TryAddAsync<ActorA>();

        await reference.Received(1).TellAsync(Arg.Any<AddActor>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddChild_Should_ReturnActorReference()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        var result = supervisor.TryAddWithResult<ActorA>();

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<ActorReference>();
    }

    [Test]
    public async Task AddChildAsync_Should_ReturnActorReference()
    {
        var supervisor = new SupervisorA(Substitute.For<ILogger<SupervisorA>>())
        {
            Context = CreateContext(),
        };

        var result = await supervisor.TryAddAsyncWithResult<ActorA>();

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<ActorReference>();
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

    private static IActorContext CreateContext(
        IActorFactory? actorFactory = null,
        IActorReference? reference = null,
        IActorReferenceFactory? referenceFactory = null,
        IActorProcessRegistry? registry = null,
        IServiceProvider? serviceProvider = null
    )
    {
        var context = Substitute.For<IActorContext>();

        var name = new Uri("trupe://localhost/123");
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
        public bool IsInitialized => Initialized;

        public ImmutableList<Child> GetChildren() => Children;

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
            await AddChildAsync<T>();
        }

        public async ValueTask<IActorReference> TryAddAsyncWithResult<T>()
            where T : IActor
        {
            return await AddChildAsync<T>();
        }

        public void TryAdd<T>()
            where T : IActor
        {
            AddChild<T>();
        }

        public IActorReference TryAddWithResult<T>()
            where T : IActor
        {
            return AddChild<T>();
        }

        public void TryAddByType(Type actorType)
        {
            AddChild(actorType);
        }

        public async ValueTask TryAddByTypeAsync(Type actorType)
        {
            await AddChildAsync(actorType);
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
            throw new NotImplementedException();
        }
    }

    public class ActorB : Actor, IHandleActorMessage<MessageA>
    {
        public ValueTask HandleAsync(
            MessageA message,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }
    }
}
