using System.Threading;
using System.Threading.Tasks;
using Trupe.Tests.Messages;

namespace Trupe.Tests.Actors;

public class SimpleTypedActor : Actor, IHandleActorMessage<SimpleMessage>
{
    public ValueTask HandleAsync(
        SimpleMessage message,
        CancellationToken cancellationToken = default
    )
    {
        message.Received = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask HandleAsync(
        object? message,
        CancellationToken cancellationToken = default
    )
    {
        if (message is SimpleMessage simpleMessage)
        {
            return HandleAsync(simpleMessage, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }
}
