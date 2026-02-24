using System;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Configurators;

namespace Trupe.Extensions;

/// <summary>
/// Extension methods for registering the Trupe actor system with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Trupe actor system services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure the actor system.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTrupe(
        this IServiceCollection services,
        Action<ActorSystemConfigurator> configure
    )
    {
        var configurator = new ActorSystemConfigurator(services);
        configure(configurator);

        return services;
    }
}
