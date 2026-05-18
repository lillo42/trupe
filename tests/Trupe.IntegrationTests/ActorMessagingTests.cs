using System.Linq;
using System.Threading.Tasks;
using Trupe.IntegrationTests.Actors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for basic actor messaging (Tell and Ask patterns).
/// </summary>
public class ActorMessagingTests
{
    [Test]
    public async Task Ask_EchoActor_ReturnsPong()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(EchoActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("hello"));

            // Assert
            await Assert.That(response).IsNotNull();
            await Assert.That(response.Payload).IsEqualTo("hello");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Ask_EchoActor_MultipleCalls_AllSucceed()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(EchoActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Act & Assert
            for (int i = 0; i < 10; i++)
            {
                var response = await actorRef.AskAsync<Pong>(new Ping($"msg-{i}"));
                await Assert.That(response.Payload).IsEqualTo($"msg-{i}");
            }
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Tell_CounterActor_ThenAsk_ReturnsCorrectCount()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(CounterActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Act - send 5 increment messages
            for (int i = 0; i < 5; i++)
            {
                await actorRef.TellAsync(new Increment());
            }

            // Give time for messages to be processed
            await Task.Delay(200);

            var result = await actorRef.AskAsync<CountResult>(new GetCount());

            // Assert
            await Assert.That(result.Count).IsEqualTo(5);
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Ask_SlowActor_WaitsForResponse()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(SlowActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("slow"));

            // Assert
            await Assert.That(response).IsNotNull();
            await Assert.That(response.Payload).IsEqualTo("slow");
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
