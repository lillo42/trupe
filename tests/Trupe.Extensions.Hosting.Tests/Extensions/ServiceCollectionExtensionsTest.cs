using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Trupe.Extensions.Hosting.Tests.Extensions;

public class ServiceCollectionExtensionsTest
{
    [Test]
    public async Task AddActorSystemHostedSevice_Should_RegisterActorSystemHostedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddActorSystemHostedSevice();

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(ActorSystemHostedService)
            );
    }

    [Test]
    public async Task AddActorSystemHostedSevice_Should_ReturnSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddActorSystemHostedSevice();

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }
}
