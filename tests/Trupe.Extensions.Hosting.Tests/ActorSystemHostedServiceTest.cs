using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trupe.Abstractions;

namespace Trupe.Extensions.Hosting.Tests;

public class ActorSystemHostedServiceTest
{
    [Test]
    public async Task StartAsync_Should_StartActorSystem()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );
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
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );
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
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );
        var service = new ActorSystemHostedService(actorSystem);

        // Act & Assert - should not throw
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_Should_ReturnCompletedTask()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );
        var service = new ActorSystemHostedService(actorSystem);

        // Act
        var task = service.StartAsync(CancellationToken.None);

        // Assert
        await Assert.That(task.IsCompleted).IsTrue();
        await service.StopAsync(CancellationToken.None);
    }
}
