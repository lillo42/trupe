using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;

namespace Trupe.Extensions.Hosting.Tests;

public class ActorSystemHostedServiceTest
{
    [Test]
    public async Task StartAsync_Should_StartActorSystem()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = CreateActorSystem(rootSupervisor);
        var service = new ActorSystemHostedService(actorSystem);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await Assert.That(rootSupervisor.Context).IsNotNull();
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_Should_StopActorSystem()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = CreateActorSystem(rootSupervisor);
        var service = new ActorSystemHostedService(actorSystem);
        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - starting again should work since it was stopped
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_WhenNotStarted_Should_NotThrow()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = CreateActorSystem(rootSupervisor);
        var service = new ActorSystemHostedService(actorSystem);

        // Act & Assert - should not throw
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_WhenCalledTwiceWithoutStop_Should_Throw()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = CreateActorSystem(rootSupervisor);
        var service = new ActorSystemHostedService(actorSystem);
        await service.StartAsync(CancellationToken.None);

        // Act & Assert
        await Assert
            .That(async () => await service.StartAsync(CancellationToken.None))
            .Throws<ActorSystemAlreadyStartedException>();

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_WhenCalledMultipleTimes_Should_NotThrow()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = CreateActorSystem(rootSupervisor);
        var service = new ActorSystemHostedService(actorSystem);
        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    private static ActorSystem CreateActorSystem(IRootSupervisor rootSupervisor)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        scopeFactory.CreateScope().Returns(scope);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        var actorReferenceFactory = Substitute.For<IActorReferenceFactory>();
        var actorReference = Substitute.For<IActorReference>();
        actorReference.StopAsync().Returns(Task.CompletedTask);
        actorReferenceFactory.Create(Arg.Any<string>(), Arg.Any<IActorProcess>()).Returns(actorReference);
        serviceProvider
            .GetService(typeof(IActorReferenceFactory))
            .Returns(actorReferenceFactory);

        return new ActorSystem(rootSupervisor, serviceProvider);
    }
}
