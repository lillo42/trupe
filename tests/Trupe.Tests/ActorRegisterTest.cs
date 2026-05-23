// using System;
// using System.Threading.Tasks;
// using NSubstitute;
// using Trupe.Abstractions;
//
// namespace Trupe.Tests;
//
// public class ActorRegisterTest
// {
//     [Test]
//     [SkipOnNativeAot]
//     public async Task Instance_Should_ReturnSameInstance()
//     {
//         // Act
//         var instance1 = ActorProcessRegistry.Instance;
//         var instance2 = ActorProcessRegistry.Instance;
//
//         // Assert
//         await Assert.That(instance1).IsSameReferenceAs(instance2);
//     }
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task Register_Should_AddActor()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var reference = Substitute.For<IActorReference>();
//         reference.Name.Returns(new Uri("trupe://localhost/actor1"));
//         var process = Substitute.For<IActorProcess>();
//
//         // Act
//         register.Register(reference, process);
//
//         // Assert
//         var result = register.Get(reference);
//         await Assert.That(result).IsSameReferenceAs(process);
//     }
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task Get_Should_ReturnProcess_WhenRegistered()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var reference = Substitute.For<IActorReference>();
//         reference.Name.Returns(new Uri("trupe://localhost/actor1"));
//         var process = Substitute.For<IActorProcess>();
//         register.Register(reference, process);
//
//         // Act
//         var result = register.Get(reference);
//
//         // Assert
//         await Assert.That(result).IsSameReferenceAs(process);
//     }
//
//     [Test]
//     public async Task Get_Should_Throw_WhenNotRegistered()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var reference = Substitute.For<IActorReference>();
//         reference.Name.Returns(new Uri("trupe://localhost/nonexistent"));
//
//         // Act & Assert
//         var act = () => register.Get(reference);
//         await Assert.That(act).Throws<Exception>();
//     }
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task GetReference_Should_ReturnReference_WhenRegistered()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var reference = Substitute.For<IActorReference>();
//         var uri = new Uri("trupe://localhost/actor1");
//         reference.Name.Returns(uri);
//         var process = Substitute.For<IActorProcess>();
//         register.Register(reference, process);
//
//         // Act
//         var result = register.GetReference(uri);
//
//         // Assert
//         await Assert.That(result).IsSameReferenceAs(reference);
//     }
//
//     [Test]
//     public async Task GetReference_Should_ReturnDeadLetter_WhenNotRegistered()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var uri = new Uri("trupe://localhost/nonexistent");
//
//         // Act
//         var result = register.GetReference(uri);
//
//         // Assert
//         await Assert.That(result).IsTypeOf<DeadLetterActorReference>();
//     }
//
//     [Test]
//     [SkipOnNativeAot]
//     public async Task Remove_Should_RemoveRegisteredActor()
//     {
//         // Arrange
//         var register = new ActorProcessRegistry();
//         var reference = Substitute.For<IActorReference>();
//         reference.Name.Returns(new Uri("trupe://localhost/actor1"));
//         var process = Substitute.For<IActorProcess>();
//         register.Register(reference, process);
//
//         // Act
//         register.Remove(reference);
//
//         // Assert
//         var act = () => register.Get(reference);
//         await Assert.That(act).Throws<Exception>();
//     }
// }
