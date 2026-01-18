using System.Threading;
using System.Threading.Tasks;

namespace Trupe;

public interface IActor
{
    IActorContext Context { get; }

    ValueTask Handle(object? message, CancellationToken cancellationToken = default);
}
