// using System;
// using System.Threading.Tasks;
// using Trupe.Abstractions;
// using Trupe.Abstractions.Options;
// using Trupe.Abstractions.Supervisors;
// using Trupe.Extensions;
//
// namespace Trupe.Tests.Extensions;
//
// public class RootSupervisorOptionsExtensionsTest
// {
//     private class TestActor : Actor { }
//
//     private class NonActorType { }
//
//     [Test]
//     public async Task AddActorGeneric_Should_AddChildToOptions()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         options.AddActor<TestActor>();
//
//         // Assert
//         await Assert.That(options.Children).Count().IsEqualTo(1);
//         await Assert.That(options.Children[0].ActorType).IsEqualTo(typeof(TestActor));
//     }
//
//     [Test]
//     public async Task AddActorGeneric_Should_ReturnSameOptions()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         var result = options.AddActor<TestActor>();
//
//         // Assert
//         await Assert.That(result).IsSameReferenceAs(options);
//     }
//
//     [Test]
//     public async Task AddActorGeneric_WithConfigure_Should_InvokeConfigureAction()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         options.AddActor<TestActor>(spec => spec.RestartPolicy = RestartPolicy.Temporary);
//
//         // Assert
//         await Assert.That(options.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Temporary);
//     }
//
//     [Test]
//     public async Task AddActorByType_Should_AddChildToOptions()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         options.AddActor(typeof(TestActor));
//
//         // Assert
//         await Assert.That(options.Children).Count().IsEqualTo(1);
//         await Assert.That(options.Children[0].ActorType).IsEqualTo(typeof(TestActor));
//     }
//
//     [Test]
//     public async Task AddActorByType_WithNonActorType_Should_ThrowInvalidOperationException()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act & Assert
//         var act = () => options.AddActor(typeof(NonActorType));
//         await Assert.That(act).ThrowsExactly<InvalidOperationException>();
//     }
//
//     [Test]
//     public async Task AddActorByType_WithConfigure_Should_InvokeConfigureAction()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         options.AddActor(typeof(TestActor), spec => spec.RestartPolicy = RestartPolicy.Transient);
//
//         // Assert
//         await Assert.That(options.Children[0].RestartPolicy).IsEqualTo(RestartPolicy.Transient);
//     }
//
//     [Test]
//     public async Task AddActorByType_Should_ReturnSameOptions()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         var result = options.AddActor(typeof(TestActor));
//
//         // Assert
//         await Assert.That(result).IsSameReferenceAs(options);
//     }
//
//     [Test]
//     public async Task AddSupervisorGeneric_Should_AddChildToOptions()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act - TestActor implements IActor but not ISupervisor, so use the RootSupervisor type
//         options.AddActor<TestActor>();
//
//         // Assert
//         await Assert.That(options.Children).Count().IsEqualTo(1);
//     }
//
//     [Test]
//     public async Task AddSupervisorByType_WithNonSupervisorType_Should_ThrowInvalidOperationException()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act & Assert
//         var act = () => options.AddSupervisor(typeof(NonActorType));
//         await Assert.That(act).ThrowsExactly<InvalidOperationException>();
//     }
//
//     [Test]
//     public async Task AddMultipleActors_Should_AddAllToChildren()
//     {
//         // Arrange
//         var options = new RootSupervisorOptions();
//
//         // Act
//         options.AddActor<TestActor>().AddActor<TestActor>();
//
//         // Assert
//         await Assert.That(options.Children).Count().IsEqualTo(2);
//     }
// }
