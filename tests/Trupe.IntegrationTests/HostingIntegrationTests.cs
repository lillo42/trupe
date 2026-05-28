using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trupe.Abstractions;
using Trupe.Extensions;
using Trupe.IntegrationTests.Actors;
using Trupe.Supervisors;

namespace Trupe.IntegrationTests;

/// <summary>
/// Integration tests for the hosted service integration (Trupe.Extensions.Hosting).
/// </summary>
public class HostingIntegrationTests
{
    [Test]
    public async Task HostedService_StartsAndStopsActorSystem()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddTrupe(cfg =>
            {
                cfg.AddActor<EchoActor>();
                cfg.ConfigureRootSupervisor(opts =>
                {
                    opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
                });
            });
            services.AddActorSystemHostedSevice();
        });

        var host = builder.Build();

        // Act
        await host.StartAsync();
        await Task.Delay(200);

        // Assert - actor system should be running, supervisor should have children
        var supervisor = host.Services.GetRequiredService<IRootSupervisor>();
        int childCount = 0;
        foreach (var _ in supervisor.Children)
        {
            childCount++;
        }
        await Assert.That(childCount).IsEqualTo(1);

        // Cleanup
        await host.StopAsync();
    }

    [Test]
    public async Task HostedService_ActorCanProcessMessages()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddTrupe(cfg =>
            {
                cfg.AddActor<EchoActor>();
                cfg.ConfigureRootSupervisor(opts =>
                {
                    opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
                });
            });
            services.AddActorSystemHostedSevice();
        });

        var host = builder.Build();
        await host.StartAsync();
        await Task.Delay(200);

        try
        {
            // Act
            var supervisor = host.Services.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();
            var response = await actorRef.AskAsync<Pong>(new Ping("hosted"));

            // Assert
            await Assert.That(response.Payload).IsEqualTo("hosted");
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
