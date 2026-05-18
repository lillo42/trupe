using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Extensions;
using Trupe.Supervisors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Helper to bootstrap an actor system for integration tests.
/// </summary>
public static class ActorSystemFixture
{
    public static (ActorSystem System, IServiceProvider Provider) Create(params Type[] actorTypes)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            foreach (var actorType in actorTypes)
            {
                cfg.AddActor(actorType);
            }

            cfg.ConfigureRootSupervisor(opts =>
            {
                foreach (var actorType in actorTypes)
                {
                    opts.Children.Add(new ChildSpecification(actorType));
                }
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        return (system, provider);
    }

    public static async Task<(
        ActorSystem System,
        IRootSupervisor Supervisor,
        IServiceProvider Provider
    )> CreateAndStartAsync(params Type[] actorTypes)
    {
        var (system, provider) = Create(actorTypes);
        await system.StartAsync();

        // Give the system a moment to initialize actors
        await Task.Delay(100);

        var supervisor = provider.GetRequiredService<IRootSupervisor>();
        return (system, supervisor, provider);
    }
}
