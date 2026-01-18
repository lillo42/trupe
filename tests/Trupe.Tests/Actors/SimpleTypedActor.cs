using System.Threading;
using System.Threading.Tasks;
using Trupe.Tests.Messages;

namespace Trupe.Tests.Actors;

public class SimpleTypedActor : Actor, IHandleActorMessage<SimpleMessage>
{
    public ValueTask Handle(SimpleMessage message, CancellationToken cancellationToken = default)
    {
        message.Received = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask Handle(object? message, CancellationToken cancellationToken = default)
    {
        if (message is SimpleMessage simpleMessage)
        {
            return Handle(simpleMessage, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }
}

