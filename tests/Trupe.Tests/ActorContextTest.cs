using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.SystemMessages;

namespace Trupe.Tests;

public class ActorContextTest
{
    [Test]
    public async Task DeathWatch_Should_ForwardTerminated()
    {
        var @ref = Substitute.For<IActorReference>();
        var otherRef = Substitute.For<IActorReference>();
        var scope = Substitute.For<IServiceScope>();

        var context = new ActorContext(@ref, scope);

        context.DeathWatch(otherRef);

        otherRef.Terminated += Raise.EventWith(
            new object(),
            new ActorReferenceTerminatedEventArgs(otherRef, TerminatedReason.Stopped)
        );

        @ref.Received(1).Tell(Arg.Is<object>(x => x is ActorTerminated));
    }

    [Test]
    public async Task UnWatchDeath_Should_NotReceivedTerminated()
    {
        var @ref = Substitute.For<IActorReference>();
        var otherRef = Substitute.For<IActorReference>();
        var scope = Substitute.For<IServiceScope>();

        var context = new ActorContext(@ref, scope);

        context.DeathWatch(otherRef);
        context.UnWatchDeath(otherRef);

        otherRef.Terminated += Raise.EventWith(
            new object(),
            new ActorReferenceTerminatedEventArgs(otherRef, TerminatedReason.Stopped)
        );

        @ref.DidNotReceive().Tell(Arg.Any<object>());
    }
}
