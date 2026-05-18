using System;
using System.Threading.Tasks;
using Trupe.IntegrationTests.Actors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for ActorSystem start/stop lifecycle.
/// </summary>
public class ActorSystemTests
{
    [Test]
    public async Task ActorSystem_StartAndStop_Succeeds()
    {
        // Arrange
        var (system, _) = ActorSystemFixture.Create(typeof(EchoActor));

        // Act & Assert - should not throw
        await system.StartAsync();
        await Task.Delay(100);
        await system.StopAsync();
    }

    [Test]
    public async Task ActorSystem_DoubleStart_ThrowsInvalidOperationException()
    {
        // Arrange
        var (system, _) = ActorSystemFixture.Create(typeof(EchoActor));
        await system.StartAsync();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await system.StartAsync()
            );
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task ActorSystem_StopWithoutStart_DoesNotThrow()
    {
        // Arrange
        var (system, _) = ActorSystemFixture.Create(typeof(EchoActor));

        // Act & Assert - should not throw
        await system.StopAsync();
    }

    [Test]
    public async Task ActorSystem_MultipleActorTypes_AllRegistered()
    {
        // Arrange & Act
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(EchoActor),
            typeof(CounterActor)
        );

        try
        {
            // Assert - supervisor should have 2 children
            int childCount = 0;
            foreach (var _ in supervisor.Children)
            {
                childCount++;
            }

            await Assert.That(childCount).IsEqualTo(2);
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
