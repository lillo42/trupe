using System.Threading;
using System.Threading.Tasks;
using Trupe.Exceptions;

namespace Trupe;

public abstract class Actor : IActor
{
    public IActorContext Context { get; internal set; } = null!;

    public virtual ValueTask Handle(object? message, CancellationToken cancellationToken = default)
    {
        throw new UnhandleMessageException(message, this);
    }
}
