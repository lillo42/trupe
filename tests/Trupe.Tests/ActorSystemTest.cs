using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trupe.Abstractions;

namespace Trupe.Tests;

public class ActorSystemTest
{
    [Test]
    [SkipOnNativeAot]
    public async Task Start_Should_SetContextOnRootSupervisor()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );

        // Act
        await actorSystem.StartAsync();

        // Assert
        await Assert.That(rootSupervisor.Context).IsNotNull();
        await actorSystem.StopAsync();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task Start_WhenAlreadyRunning_Should_ThrowInvalidOperationException()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );

        await actorSystem.StartAsync();

        // Act & Assert
        var act = async () => await actorSystem.StartAsync();
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
        await actorSystem.StopAsync();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task StopAsync_WhenRunning_Should_AllowRestartAfterStop()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );

        await actorSystem.StartAsync();

        // Act
        await actorSystem.StopAsync();

        // Assert - Starting again should work since it's stopped
        await actorSystem.StartAsync();
        await actorSystem.StopAsync();
    }

    [Test]
    public async Task StopAsync_WhenNotRunning_Should_NotThrow()
    {
        // Arrange
        var rootSupervisor = Substitute.For<IRootSupervisor>();
        var actorSystem = new ActorSystem(
            rootSupervisor,
            new ServiceCollection().BuildServiceProvider()
        );

        // Act & Assert - should not throw
        await actorSystem.StopAsync();
    }
}
