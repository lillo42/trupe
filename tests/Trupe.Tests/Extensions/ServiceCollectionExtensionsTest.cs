using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Extensions;

namespace Trupe.Tests.Extensions;

public class ServiceCollectionExtensionsTest
{
    [Test]
    public async Task AddTrupe_Should_ReturnSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTrupe(_ => { });

        // Assert
        await Assert.That(result).IsSameReferenceAs(services);
    }

    [Test]
    public async Task AddTrupe_Should_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configured = false;

        // Act
        services.AddTrupe(_ => configured = true);

        // Assert
        await Assert.That(configured).IsTrue();
    }

    [Test]
    public async Task AddTrupe_Should_RegisterActorSystem()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTrupe(_ => { });

        // Assert
        await Assert
            .That(services)
            .Contains(x => x.ServiceType == typeof(ActorSystem));
    }
}
