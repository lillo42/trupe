using Trupe.Tests.Messages;

namespace Trupe.Tests.Actors;

public class SimpleTypedActor : IActor<SimpleMessage>
{
    public ValueTask ReceiveAsync(SimpleMessage message, CancellationToken cancellationToken = default)
    {
        message.Received = true;
        return ValueTask.CompletedTask;
    }

    public IActorContext? Context { get; set; }
    public ValueTask ReceiveAsync(object? message, CancellationToken cancellationToken = default)
    {
        if (message is SimpleMessage simpleMessage)
        {
            return ReceiveAsync(simpleMessage, cancellationToken);    
        }
            
        return ValueTask.CompletedTask;
    }
}