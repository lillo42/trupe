using System.Threading;
using System.Threading.Tasks;
using Trupe.Abstractions;

namespace Trupe.IntegrationTests.Actors;

public record Ping(string Payload);

public record Pong(string Payload);

public record Increment;

public record GetCount;

public record CountResult(int Count);

/// <summary>
/// Simple echo actor that responds to Ping with Pong.
/// </summary>
public class EchoActor : Actor, IHandleActorMessage<Ping>
{
    public ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
    {
        Context.Response = new Pong(message.Payload);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Stateful counter actor that increments on each message and can report count.
/// </summary>
public class CounterActor : Actor, IHandleActorMessage<Increment>, IHandleActorMessage<GetCount>
{
    private int _count;

    public ValueTask HandleAsync(Increment message, CancellationToken cancellationToken = default)
    {
        _count++;
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GetCount message, CancellationToken cancellationToken = default)
    {
        Context.Response = new CountResult(_count);
        return ValueTask.CompletedTask;
    }
}

public record ThrowError(string Message);

/// <summary>
/// Actor that always throws when handling a ThrowError message.
/// </summary>
public class FailingActor : Actor, IHandleActorMessage<ThrowError>, IHandleActorMessage<Ping>
{
    public bool WasRestarted { get; private set; }

    public override ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
    {
        WasRestarted = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(ThrowError message, CancellationToken cancellationToken = default)
    {
        throw new System.InvalidOperationException(message.Message);
    }

    public ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
    {
        Context.Response = new Pong(message.Payload);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Actor that tracks lifecycle events.
/// </summary>
public class LifecycleActor : Actor, IHandleActorMessage<Ping>
{
    public static bool Initialized { get; set; }
    public static bool BeforeRestart { get; set; }
    public static bool AfterRestart { get; set; }

    public static void Reset()
    {
        Initialized = false;
        BeforeRestart = false;
        AfterRestart = false;
    }

    public override ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        Initialized = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask BeforeRestartAsync(CancellationToken cancellationToken = default)
    {
        BeforeRestart = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask AfterRestartAsync(CancellationToken cancellationToken = default)
    {
        AfterRestart = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
    {
        Context.Response = new Pong(message.Payload);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Actor that simulates slow work.
/// </summary>
public class SlowActor : Actor, IHandleActorMessage<Ping>
{
    public async ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        Context.Response = new Pong(message.Payload);
    }
}
