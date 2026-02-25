using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trupe.Configurators;

namespace Trupe.Extensions.Hosting.Tests.Extensions;

public class ActorSystemConfiguratorExtensionsTest
{
    [Test]
    public async Task AddHostedService_Should_RegisterActorSystemHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        configurator.AddHostedService();

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(ActorSystemHostedService)
            );
    }

    [Test]
    public async Task AddHostedService_Should_ReturnSameConfigurator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        var result = configurator.AddHostedService();

        // Assert
        await Assert.That(result).IsSameReferenceAs(configurator);
    }
}
