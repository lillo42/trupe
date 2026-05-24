using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.Extensions;
using Trupe.Abstractions;
using Trupe.Abstractions.SystemMessages;

namespace Trupe.Tests;

public class ActorContextTest
{
    [Test]
    public async Task DeathWatch_Should_CallRefRegister()
    {
        var @ref = Substitute.For<IActorReference>();
        var scope = Substitute.For<IServiceScope>();
        var context = new ActorContext(@ref, scope);

        var disposable = Substitute.For<IDisposable>();
        var otherRef = Substitute.For<IActorReference>();
        otherRef.Register(Arg.Any<IActorReferenceListener>()).Returns(disposable);

        context.DeathWatch(otherRef);

        otherRef.Received(1).Register(Arg.Is(context));
    }

    [Test]
    public async Task UnDeathWatch_Should_CallDispose()
    {
        var @ref = Substitute.For<IActorReference>();
        var scope = Substitute.For<IServiceScope>();
        var context = new ActorContext(@ref, scope);

        var disposable = Substitute.For<IDisposable>();
        var otherRef = Substitute.For<IActorReference>();
        otherRef.Register(Arg.Any<IActorReferenceListener>()).Returns(disposable);

        context.DeathWatch(otherRef);
        context.UnWatchDeath(otherRef);

        otherRef.Received(1).Register(Arg.Is(context));
        disposable.Received(1).Dispose();
    }

    [Test]
    public async Task OnTerminated_Should_SelfTell()
    {
        var @ref = Substitute.For<IActorReference>();
        var scope = Substitute.For<IServiceScope>();
        var context = new ActorContext(@ref, scope);

        var otherRef = Substitute.For<IActorReference>();
        context.OnTerminated(otherRef, TerminatedReason.Stopped);

        @ref.Received(1)
            .Tell(
                Arg.Is<object>(x =>
                    x is ActorTerminated
                    && ((ActorTerminated)x).Reference == otherRef
                    && ((ActorTerminated)x).Reason == TerminatedReason.Stopped
                ),
                Arg.Any<TimeSpan?>()
            );
    }
}
