using System.Linq;
using System.Threading.Tasks;
using Trupe.IntegrationTests.Actors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for actor lifecycle hooks (Initialize, BeforeRestart, AfterRestart).
/// </summary>
public class ActorLifecycleTests
{
    [Test]
    public async Task Actor_InitializeAsync_IsCalledOnStart()
    {
        // Arrange
        LifecycleActor.Reset();

        // Act
        var (system, _, _) = await ActorSystemFixture.CreateAndStartAsync(typeof(LifecycleActor));
        try
        {
            // Give time for initialization
            await Task.Delay(200);

            // Assert
            await Assert.That(LifecycleActor.Initialized).IsTrue();
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Actor_AfterRestart_IsCalledAfterFailure()
    {
        // Arrange
        LifecycleActor.Reset();
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(LifecycleActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Confirm actor is alive
            var response = await actorRef.AskAsync<Pong>(new Ping("before-fail"));
            await Assert.That(response.Payload).IsEqualTo("before-fail");

            // Reset after initial init
            LifecycleActor.Reset();

            // Act - send an unhandled message type to trigger failure
            actorRef.Tell("unhandled-message-causes-failure");

            // Give time for supervisor to restart actor
            await Task.Delay(500);

            // Assert - lifecycle hooks should have been called
            await Assert.That(LifecycleActor.BeforeRestart).IsTrue();
            await Assert.That(LifecycleActor.AfterRestart).IsTrue();
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
