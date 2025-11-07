using System.Threading.Channels;

namespace Trupe.Mailboxes;

public class ChannelMailBox : IMailbox
{
    private readonly Channel<IMessage> _channel;
    public ChannelMailBox()
        : this(0)
    {
        
    }

    public ChannelMailBox(int maxSize, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
    {
        if (maxSize == 0)
        {
            _channel = Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }
        else
        {
            _channel = Channel.CreateBounded<IMessage>(new BoundedChannelOptions(maxSize)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = fullMode 
            });
        }
    }
    
    public void Enqueue(IMessage message)
    {
        EnqueueAsync(message, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async ValueTask<IMessage> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}