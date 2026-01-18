using System.Threading;
using System.Threading.Tasks;
using Trupe.Tests.Messages;

namespace Trupe.Tests.Actors;

public class SimpleActor : Actor
{
    public override ValueTask Handle(object? message, CancellationToken cancellationToken = default)
    {
        if (message is SimpleMessage simpleMessage)
        {
            simpleMessage.Received = true;
        }

        return ValueTask.CompletedTask;
    }
}

