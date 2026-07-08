using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;

namespace Trupe.Tests;

public class ActorSystemTest
{
    [Test]
    public async Task Start_Should_StartRootSupervisor()
    {
        var root = Substitute.For<IRootSupervisor>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        scopeFactory.CreateScope().Returns(scope);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var refFactory = Substitute.For<IActorReferenceFactory>();
        var @ref = Substitute.For<IActorReference>();
        refFactory.Create(Arg.Any<string>(), Arg.Any<IActorProcess>()).Returns(@ref);

        serviceProvider.GetService(typeof(IActorReferenceFactory)).Returns(refFactory);

        var system = new ActorSystem(root, serviceProvider);

        await system.StartAsync();

        _ = refFactory.Received(1).Create(Arg.Any<string>(), Arg.Any<IActorProcess>());

        await system.StopAsync();
    }

    [Test]
    public async Task Start_Should_Throw_When_ItsAlreadyStarted()
    {
        var root = Substitute.For<IRootSupervisor>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        scopeFactory.CreateScope().Returns(scope);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var refFactory = Substitute.For<IActorReferenceFactory>();
        var @ref = Substitute.For<IActorReference>();
        refFactory.Create(Arg.Any<string>(), Arg.Any<IActorProcess>()).Returns(@ref);

        serviceProvider.GetService(typeof(IActorReferenceFactory)).Returns(refFactory);

        var system = new ActorSystem(root, serviceProvider);

        await system.StartAsync();

        await Assert.That(system.StartAsync).Throws<ActorSystemAlreadyStartedException>();

        await system.StopAsync();
    }
}
