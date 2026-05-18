using System.Linq;
using System.Threading.Tasks;
using Trupe.IntegrationTests.Actors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for supervisor failure handling and actor restart.
/// </summary>
public class SupervisionTests
{
    [Test]
    public async Task Supervisor_RestartsActor_AfterFailure()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(FailingActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Verify actor is alive
            var response = await actorRef.AskAsync<Pong>(new Ping("alive"));
            await Assert.That(response.Payload).IsEqualTo("alive");

            // Act - cause failure
            actorRef.Tell(new ThrowError("test failure"));

            // Wait for restart
            await Task.Delay(500);

            // Assert - actor should be alive again after restart
            var response2 = await actorRef.AskAsync<Pong>(new Ping("still-alive"));
            await Assert.That(response2.Payload).IsEqualTo("still-alive");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Supervisor_MultipleFailures_ActorKeepsRestarting()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(FailingActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Cause multiple failures
            for (int i = 0; i < 3; i++)
            {
                actorRef.Tell(new ThrowError($"failure-{i}"));
                await Task.Delay(300);
            }

            // Wait for restart
            await Task.Delay(500);

            // Actor should still be alive
            var response = await actorRef.AskAsync<Pong>(new Ping("recovered"));
            await Assert.That(response.Payload).IsEqualTo("recovered");
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
