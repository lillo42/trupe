using Trupe.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register the actor system hosted service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ActorSystemHostedService"/> so the actor system is automatically
    /// started and stopped with the application host.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The same <paramref name="services"/> instance for method chaining.</returns>
    public static IServiceCollection AddActorSystemHostedSevice(this IServiceCollection services)
    {
        services.AddHostedService<ActorSystemHostedService>();
        return services;
    }
}
