using Microsoft.Extensions.DependencyInjection;
using Trupe.Extensions.Hosting;

namespace Trupe.Configurators;

/// <summary>
/// Extension methods for <see cref="ActorSystemConfigurator"/> to integrate with the .NET hosting infrastructure.
/// </summary>
public static class ActorSystemConfiguratorExtensions
{
    /// <summary>
    /// Registers the <see cref="ActorSystemHostedService"/> so the actor system is automatically
    /// started and stopped with the application host.
    /// </summary>
    /// <param name="configurator">The <see cref="ActorSystemConfigurator"/> to configure.</param>
    /// <returns>The same <paramref name="configurator"/> instance for method chaining.</returns>
    public static ActorSystemConfigurator AddHostedService(
        this ActorSystemConfigurator configurator
    )
    {
        configurator.Services.AddHostedService<ActorSystemHostedService>();
        return configurator;
    }
}
