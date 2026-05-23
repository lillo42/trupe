// using System.Threading.Tasks;
// using Trupe.Abstractions;
// using Trupe.Abstractions.Extensions;
//
// namespace Trupe.Tests.Extensions;
//
// public class TypeExtensionsTest
// {
//     private class TestActor : Actor { }
//
//     private class PlainClass { }
//
//     [Test]
//     public async Task IsActor_WithActorType_Should_ReturnTrue()
//     {
//         await Assert.That(typeof(TestActor).IsActor()).IsTrue();
//     }
//
//     [Test]
//     public async Task IsActor_WithNonActorType_Should_ReturnFalse()
//     {
//         await Assert.That(typeof(PlainClass).IsActor()).IsFalse();
//     }
//
//     [Test]
//     public async Task IsActor_WithIActorInterface_Should_ReturnTrue()
//     {
//         await Assert.That(typeof(IActor).IsActor()).IsTrue();
//     }
//
//     [Test]
//     public async Task IsSupervisor_WithSupervisorInterface_Should_ReturnTrue()
//     {
//         await Assert.That(typeof(ISupervisor).IsSupervisor()).IsTrue();
//     }
//
//     [Test]
//     public async Task IsSupervisor_WithNonSupervisorType_Should_ReturnFalse()
//     {
//         await Assert.That(typeof(PlainClass).IsSupervisor()).IsFalse();
//     }
//
//     [Test]
//     public async Task IsSupervisor_WithActorType_Should_ReturnFalse()
//     {
//         await Assert.That(typeof(TestActor).IsSupervisor()).IsFalse();
//     }
//
//     [Test]
//     public async Task IsRootSupervisor_WithRootSupervisorInterface_Should_ReturnTrue()
//     {
//         await Assert.That(typeof(IRootSupervisor).IsRootSupervisor()).IsTrue();
//     }
//
//     [Test]
//     public async Task IsRootSupervisor_WithNonRootSupervisorType_Should_ReturnFalse()
//     {
//         await Assert.That(typeof(PlainClass).IsRootSupervisor()).IsFalse();
//     }
//
//     [Test]
//     public async Task IsRootSupervisor_WithSupervisorInterface_Should_ReturnFalse()
//     {
//         await Assert.That(typeof(ISupervisor).IsRootSupervisor()).IsFalse();
//     }
// }
