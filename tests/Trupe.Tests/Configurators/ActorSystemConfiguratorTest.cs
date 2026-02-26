using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Configurators;

namespace Trupe.Tests.Configurators;

public class ActorSystemConfiguratorTest
{
    private class TestActor : Actor { }

    private class NonActorType { }

    [Test]
    public async Task Constructor_Should_RegisterActorSystem()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        _ = new ActorSystemConfigurator(services);

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(ActorSystem) && x.Lifetime == ServiceLifetime.Singleton
            );
    }

    [Test]
    public async Task Constructor_Should_RegisterDefaultRootSupervisor()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        _ = new ActorSystemConfigurator(services);

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(IRootSupervisor) && x.Lifetime == ServiceLifetime.Singleton
            );
    }

    [Test]
    public async Task AddActorGeneric_Should_RegisterActorAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        configurator.AddActor<TestActor>();

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(TestActor) && x.Lifetime == ServiceLifetime.Transient
            );
    }

    [Test]
    public async Task AddActorGeneric_Should_ReturnSameConfigurator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        var result = configurator.AddActor<TestActor>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(configurator);
    }

    [Test]
    public async Task AddActorByType_Should_RegisterActorAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        configurator.AddActor(typeof(TestActor));

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(TestActor) && x.Lifetime == ServiceLifetime.Transient
            );
    }

    [Test]
    public async Task AddActorByType_WithNonActorType_Should_ThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act & Assert
        var act = () => configurator.AddActor(typeof(NonActorType));
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task AddSupervisorGeneric_Should_RegisterSupervisorAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        configurator.AddActor<TestActor>();

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(TestActor) && x.Lifetime == ServiceLifetime.Transient
            );
    }

    [Test]
    public async Task ConfigureRootSupervisor_Should_ReturnSameConfigurator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        var result = configurator.ConfigureRootSupervisor(_ => { });

        // Assert
        await Assert.That(result).IsSameReferenceAs(configurator);
    }

    [Test]
    public async Task SetRootSupervisorGeneric_Should_RegisterCustomRootSupervisor()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        configurator.SetRootSupervisor<RootSupervisor>();

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(IRootSupervisor) && x.Lifetime == ServiceLifetime.Singleton
            );
    }

    [Test]
    public async Task SetRootSupervisorGeneric_Should_ReturnSameConfigurator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);

        // Act
        var result = configurator.SetRootSupervisor<RootSupervisor>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(configurator);
    }

    [Test]
    public async Task Constructor_Should_RegisterDefaultActorRegister()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        _ = new ActorSystemConfigurator(services);

        // Assert
        await Assert
            .That(services)
            .Contains(x =>
                x.ServiceType == typeof(IActorRegister) && x.Lifetime == ServiceLifetime.Singleton
            );
    }

    [Test]
    [SkipOnNativeAot]
    public async Task SetActorRegister_Should_RegisterCustomActorRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);
        var customRegister = NSubstitute.Substitute.For<IActorRegister>();

        // Act
        configurator.SetActorRegister(customRegister);

        // Assert
        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IActorRegister>();
        await Assert.That(resolved).IsSameReferenceAs(customRegister);
    }

    [Test]
    [SkipOnNativeAot]
    public async Task SetActorRegister_Should_ReturnSameConfigurator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configurator = new ActorSystemConfigurator(services);
        var customRegister = NSubstitute.Substitute.For<IActorRegister>();

        // Act
        var result = configurator.SetActorRegister(customRegister);

        // Assert
        await Assert.That(result).IsSameReferenceAs(configurator);
    }
}
