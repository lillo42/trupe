using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
        
        // Waiting the message to be propagated
        await Task.Delay(TimeSpan.FromSeconds(1));

        await @ref.Received(1)
            .TellAsync(Arg.Is<object>(x =>
                    x is ActorTerminated
                    && ((ActorTerminated)x).Reference == otherRef
                    && ((ActorTerminated)x).Reason == TerminatedReason.Stopped),
                Arg.Any<Dictionary<string, object?>?>(),
                Arg.Any<CancellationToken>());
    }
}
