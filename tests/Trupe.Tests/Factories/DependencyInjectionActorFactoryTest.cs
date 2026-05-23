// using System.Threading.Tasks;
// using Microsoft.Extensions.DependencyInjection;
// using Trupe.Abstractions;
// using Trupe.Factories;
//
// namespace Trupe.Tests.Factories;
//
// public class DependencyInjectionActorFactoryTest
// {
//     private class TestActor : Actor { }
//
//     [Test]
//     public async Task CreateActor_Should_ResolveActorFromServiceProvider()
//     {
//         // Arrange
//         var services = new ServiceCollection();
//         services.AddTransient<TestActor>();
//         var serviceProvider = services.BuildServiceProvider();
//         var factory = new ActorFactory(serviceProvider);
//
//         // Act
//         var actor = factory.CreateActor(typeof(TestActor));
//
//         // Assert
//         await Assert.That(actor).IsNotNull();
//         await Assert.That(actor).IsTypeOf<TestActor>();
//     }
//
//     [Test]
//     public async Task CreateActor_WithUnregisteredType_Should_Throw()
//     {
//         // Arrange
//         var services = new ServiceCollection();
//         var serviceProvider = services.BuildServiceProvider();
//         var factory = new ActorFactory(serviceProvider);
//
//         // Act & Assert
//         var act = () => factory.CreateActor(typeof(TestActor));
//         await Assert.That(act).ThrowsException();
//     }
// }
