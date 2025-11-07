namespace Trupe;

public interface IActor
{
    IActorContext? Context { get; set; }
    
    ValueTask ReceiveAsync(object? message, CancellationToken cancellationToken = default);
}

public interface IActor<in TMessage> : IActor
{
    ValueTask ReceiveAsync(TMessage message, CancellationToken cancellationToken = default);
}
