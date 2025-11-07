using Trupe.Tests.Messages;

namespace Trupe.Tests.Actors;

public class SimpleActor : IActor
{
    public IActorContext? Context { get; set; }
    public ValueTask ReceiveAsync(object? message, CancellationToken cancellationToken = default)
    {
        if (message is SimpleMessage simpleMessage)
        {
            simpleMessage.Received = true;
        }
            
        return ValueTask.CompletedTask;
    }
}