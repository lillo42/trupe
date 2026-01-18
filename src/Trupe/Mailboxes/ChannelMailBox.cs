using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Trupe.Mailboxes;

public class ChannelMailBox : IMailbox
{
    private readonly Channel<IMessage> _channel;

    public ChannelMailBox()
        : this(-1) { }

    public ChannelMailBox(
        int maxSize,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait
    )
    {
        if (maxSize <= 0)
        {
            _channel = Channel.CreateUnbounded<IMessage>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        }
        else
        {
            _channel = Channel.CreateBounded<IMessage>(
                new BoundedChannelOptions(maxSize)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = fullMode,
                }
            );
        }
    }

    public async ValueTask EnqueueAsync(
        IMessage message,
        CancellationToken cancellationToken = default
    )
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public IAsyncEnumerator<IMessage> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return _channel.Reader.ReadAllAsync().GetAsyncEnumerator(cancellationToken);
    }
}
