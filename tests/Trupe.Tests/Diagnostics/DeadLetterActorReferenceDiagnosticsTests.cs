using System;
using System.Linq;
using System.Threading.Tasks;

namespace Trupe.Tests.Diagnostics;

public class DeadLetterActorReferenceDiagnosticsTests
{
    private static readonly Uri ActorUri = new("trupe://localhost/dead-letter-test");

    [Test]
    public async Task Tell_Should_Record_DeadLetterCounter()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        try { @ref.Tell(new object()); } catch (NotImplementedException) { }

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "actor-reference.dead-letter");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Tell_Should_Tag_Operation_AsTell()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        try { @ref.Tell(new object()); } catch (NotImplementedException) { }

        var measurement = collector.Measurements.First(m => m.Name == "actor-reference.dead-letter");
        var operationTag = measurement.Tags.FirstOrDefault(t => t.Key == "operation");
        await Assert.That((string?)operationTag.Value).IsEqualTo("tell");
    }

    [Test]
    public async Task Tell_Should_Tag_Actor()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        try { @ref.Tell(new object()); } catch (NotImplementedException) { }

        var measurement = collector.Measurements.First(m => m.Name == "actor-reference.dead-letter");
        var actorTag = measurement.Tags.FirstOrDefault(t => t.Key == "actor");
        await Assert.That(actorTag.Value).IsEqualTo(ActorUri);
    }

    [Test]
    public async Task TellAsync_Should_Record_DeadLetterCounter()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        await @ref.TellAsync(new object());

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "actor-reference.dead-letter");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Ask_Should_Record_DeadLetterCounter()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        @ref.Ask<object>(new object());

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "actor-reference.dead-letter");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Ask_Should_Tag_Operation_AsAsk()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        @ref.Ask<object>(new object());

        var measurement = collector.Measurements.First(m => m.Name == "actor-reference.dead-letter");
        var operationTag = measurement.Tags.FirstOrDefault(t => t.Key == "operation");
        await Assert.That((string?)operationTag.Value).IsEqualTo("ask");
    }

    [Test]
    public async Task AskAsync_Should_Record_DeadLetterCounter()
    {
        using var collector = new MetricsCollector();
        var @ref = new DeadLetterActorReference(ActorUri);

        await @ref.AskAsync<object>(new object());

        var measurement = collector.Measurements.FirstOrDefault(m => m.Name == "actor-reference.dead-letter");
        await Assert.That(measurement).IsNotNull();
        await Assert.That(measurement!.Value).IsEqualTo(1);
    }
}
