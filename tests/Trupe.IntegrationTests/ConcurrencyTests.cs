using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trupe.IntegrationTests.Actors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for concurrent message processing.
/// </summary>
public class ConcurrencyTests
{
    [Test]
    public async Task ConcurrentAsks_AllGetCorrectResponses()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(EchoActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();

            // Act - send many asks concurrently
            var tasks = new List<Task<Pong>>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(actorRef.AskAsync<Pong>(new Ping($"concurrent-{i}")));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - all should have responded
            await Assert.That(results.Length).IsEqualTo(50);
            for (int i = 0; i < 50; i++)
            {
                await Assert.That(results[i].Payload).IsEqualTo($"concurrent-{i}");
            }
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task ConcurrentTells_CounterActor_ProcessesAll()
    {
        // Arrange
        var (system, supervisor, _) = await ActorSystemFixture.CreateAndStartAsync(
            typeof(CounterActor)
        );
        try
        {
            var actorRef = supervisor.Children.First();
            int messageCount = 100;

            // Act - send many tells concurrently
            var tasks = new List<ValueTask>();
            for (int i = 0; i < messageCount; i++)
            {
                tasks.Add(actorRef.TellAsync(new Increment()));
            }

            foreach (var t in tasks)
            {
                await t;
            }

            // Wait for processing
            await Task.Delay(500);

            var result = await actorRef.AskAsync<CountResult>(new GetCount());

            // Assert
            await Assert.That(result.Count).IsEqualTo(messageCount);
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
