using System;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;

namespace Trupe.Tests;

public class ActorRegisterTest
{
    [Test]
    [SkipOnNativeAot]
    public async Task Instance_Should_ReturnSameInstance()
    {
        // Act
        var instance1 = ActorRegister.Instance;
        var instance2 = ActorRegister.Instance;

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    [SkipOnNativeAot]
    public async Task Register_Should_AddActor()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();

        // Act
        register.Register("actor1", actor);

        // Assert
        await Assert.That(register.Contains("actor1")).IsTrue();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task Register_WithDuplicateId_Should_ThrowInvalidOperationException()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();
        register.Register("actor1", actor);

        // Act & Assert
        var act = () => register.Register("actor1", actor);
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task TryRegister_Should_ReturnTrue_WhenNotRegistered()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();

        // Act
        var result = register.TryRegister("actor1", actor);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task TryRegister_Should_ReturnFalse_WhenAlreadyRegistered()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();
        register.Register("actor1", actor);

        // Act
        var result = register.TryRegister("actor1", actor);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task Get_Should_ReturnActor_WhenRegistered()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();
        register.Register("actor1", actor);

        // Act
        var result = register.Get("actor1");

        // Assert
        await Assert.That(result).IsSameReferenceAs(actor);
    }

    [Test]
    public async Task Get_Should_ReturnNull_WhenNotRegistered()
    {
        // Arrange
        var register = new ActorRegister();

        // Act
        var result = register.Get("nonexistent");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task TryGet_Should_ReturnTrue_AndSetActor_WhenRegistered()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();
        register.Register("actor1", actor);

        // Act
        var result = register.TryGet("actor1", out var found);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(found).IsSameReferenceAs(actor);
    }

    [Test]
    public async Task TryGet_Should_ReturnFalse_WhenNotRegistered()
    {
        // Arrange
        var register = new ActorRegister();

        // Act
        var result = register.TryGet("nonexistent", out var found);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(found).IsNull();
    }

    [Test]
    [SkipOnNativeAot]
    public async Task Contains_Should_ReturnTrue_WhenRegistered()
    {
        // Arrange
        var register = new ActorRegister();
        var actor = Substitute.For<IActorReference>();
        register.Register("actor1", actor);

        // Act & Assert
        await Assert.That(register.Contains("actor1")).IsTrue();
    }

    [Test]
    public async Task Contains_Should_ReturnFalse_WhenNotRegistered()
    {
        // Arrange
        var register = new ActorRegister();

        // Act & Assert
        await Assert.That(register.Contains("nonexistent")).IsFalse();
    }
}
