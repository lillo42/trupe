namespace Trupe;

public interface IMailbox
{
    void Enqueue(IMessage message);
    ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken = default);
    
    ValueTask<IMessage> DequeueAsync(CancellationToken cancellationToken = default);
}