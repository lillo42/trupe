using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

public interface IHandleActorMessage<TMessage>
{
    ValueTask Handle(TMessage message, CancellationToken cancellationToken = default);
}
