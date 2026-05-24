using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Trupe.Abstractions;

namespace Trupe.Tests;

public class ActorReferences
{
    [Test]
    public void Tell_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        var req = new SomeRequest();
        var timeout = TimeSpan.FromSeconds(1);

        @ref.Tell(req, timeout);
        inner.Received(1).Tell(req, timeout);

        var metadata = new Dictionary<string, object?> { ["Some"] = new object() };
        @ref.Tell(req, metadata, timeout);
        inner.Received(1).Tell(req, metadata, timeout);
    }

    [Test]
    public async Task TellAsync_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();

        var @ref = new ActorReference(inner);

        var req = new SomeRequest();
        var token = new CancellationToken();

        await @ref.TellAsync(req, token);
        await inner.Received(1).TellAsync(req, token);

        var metadata = new Dictionary<string, object?> { ["Some"] = new object() };
        await @ref.TellAsync(req, metadata, token);
        await inner.Received(1).TellAsync(req, metadata, token);
    }

    [Test]
    public async Task Ask_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();

        var response = new SomeResponse();

        inner.Ask<SomeResponse>(Arg.Any<object>(), Arg.Any<TimeSpan>()).Returns(response);
        inner
            .Ask<SomeResponse>(
                Arg.Any<object>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<TimeSpan>()
            )
            .Returns(response);

        var @ref = new ActorReference(inner);

        var req = new SomeRequest();
        var timeout = TimeSpan.FromSeconds(1);

        var resp = @ref.Ask<SomeResponse>(req, timeout);
        inner.Received(1).Ask<SomeResponse>(req, timeout);
        await Assert.That(resp).IsEqualTo(response);

        var metadata = new Dictionary<string, object?> { ["Some"] = new object() };
        resp = @ref.Ask<SomeResponse>(req, metadata, timeout);
        inner.Received(1).Ask<SomeResponse>(req, metadata, timeout);
        await Assert.That(resp).IsEqualTo(response);
    }

    [Test]
    public async Task AskAsync_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();

        var response = new SomeResponse();

        inner
            .AskAsync<SomeResponse>(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(response);

        inner
            .AskAsync<SomeResponse>(
                Arg.Any<object>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(response);

        var @ref = new ActorReference(inner);

        var req = new SomeRequest();
        var token = new CancellationToken();

        var resp = await @ref.AskAsync<SomeResponse>(req, token);
        await inner.Received(1).AskAsync<SomeResponse>(req, token);
        await Assert.That(resp).IsEqualTo(response);

        var metadata = new Dictionary<string, object?> { ["Some"] = new object() };
        resp = await @ref.AskAsync<SomeResponse>(req, metadata, token);
        await inner.Received(1).AskAsync<SomeResponse>(req, metadata, token);
        await Assert.That(resp).IsEqualTo(response);
    }

    [Test]
    public void Stop_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        @ref.Stop();

        inner.Received(1).Stop();
    }

    [Test]
    public async Task StopAsync_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        await @ref.StopAsync();

        await inner.Received(1).StopAsync();
    }

    [Test]
    public async Task KillAsync_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        await @ref.KillAsync();

        await inner.Received(1).KillAsync();
    }

    [Test]
    public void MarkAsTerminate_Should_ForwardToInner()
    {
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        @ref.MarkAsTerminate(TerminatedReason.Stopped);

        inner.Received(1).MarkAsTerminate(TerminatedReason.Stopped);
    }

    [Test]
    public async Task Register_Should_InvokeTheListener_When_OnTerminatedIsCalled()
    {
        var listener = Substitute.For<IActorReferenceListener>();
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        @ref.Register(listener);
        @ref.OnTerminated(inner, TerminatedReason.Stopped);

        listener.Received(1).OnTerminated(Arg.Is(@ref), TerminatedReason.Stopped);
    }

    [Test]
    public async Task UnRegister_Should_NotInvokeTheListener_When_OnTerminatedIsCalled()
    {
        var listener = Substitute.For<IActorReferenceListener>();
        var inner = Substitute.For<IActorReference>();
        var @ref = new ActorReference(inner);

        @ref.Register(listener);
        @ref.UnRegister(listener);
        @ref.OnTerminated(inner, TerminatedReason.Stopped);

        listener
            .DidNotReceive()
            .OnTerminated(Arg.Any<IActorReference>(), Arg.Any<TerminatedReason>());
    }

    public record SomeRequest();

    public record SomeResponse();
}
