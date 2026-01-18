using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

public interface IMailbox : IAsyncEnumerable<IMessage>
{
    ValueTask EnqueueAsync(IMessage message, CancellationToken cancellationToken = default);
}
