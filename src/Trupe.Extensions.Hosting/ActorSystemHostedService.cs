using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Trupe.Extensions.Hosting;

/// <summary>
/// An <see cref="IHostedService"/> implementation that manages the lifecycle of an <see cref="ActorSystem"/>.
/// Starts the actor system when the host starts and gracefully stops it when the host shuts down.
/// </summary>
/// <param name="system">The <see cref="ActorSystem"/> instance to manage.</param>
public class ActorSystemHostedService(ActorSystem system) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        system.StartAsync();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await system.StopAsync();
    }
}
