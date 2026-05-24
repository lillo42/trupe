using System;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Exceptions;

namespace Trupe.Tests;

public class ActorProcessRegistryTest
{
    [Test]
    public async Task Register_Should_RegisterRefAndProcess()
    {
        var registry = new ActorProcessRegistry();

        var name = new Uri("trupe://localhost/456");
        var @ref = Substitute.For<IActorReference>();
        @ref.Name.Returns(name);

        var process = Substitute.For<IActorProcess>();

        registry.Register(@ref, process);

        var resolvedProcess = registry.GetProcess(@ref);
        var resolvedRef = registry.GetReference(new Uri("trupe://localhost/456"));

        await Assert.That(resolvedProcess).IsEqualTo(process);
        await Assert.That(resolvedRef).IsEqualTo(@ref);
    }

    [Test]
    public async Task GetReference_Should_ReturnDeadLetter_When_ReferenceNotExists()
    {
        var registry = new ActorProcessRegistry();

        var resolvedRef = registry.GetReference(new Uri("trupe://localhost/789"));

        await Assert.That(resolvedRef).IsAssignableTo<DeadLetterActorReference>();
    }

    [Test]
    [DependsOn(nameof(Register_Should_RegisterRefAndProcess))]
    [DependsOn(nameof(GetReference_Should_ReturnDeadLetter_When_ReferenceNotExists))]
    public async Task UnRegister_Should_UnRegisterRefAndProcess()
    {
        var registry = new ActorProcessRegistry();

        var name = new Uri("trupe://localhost/456");
        var @ref = Substitute.For<IActorReference>();
        @ref.Name.Returns(name);

        var process = Substitute.For<IActorProcess>();

        registry.Register(@ref, process);
        registry.UnRegister(@ref);

        var resolvedRef = registry.GetReference(new Uri("trupe://localhost/456"));
        await Assert.That(resolvedRef).IsAssignableTo<DeadLetterActorReference>();
    }

    [Test]
    public async Task UnRegister_Should_NotReturnError_When_RefNotExists()
    {
        var registry = new ActorProcessRegistry();

        var name = new Uri("trupe://localhost/456");
        var @ref = Substitute.For<IActorReference>();
        @ref.Name.Returns(name);

        await Assert.That(() => registry.UnRegister(@ref)).ThrowsNothing();
    }

    [Test]
    public async Task GetProcess_Should_Throw_When_ItIsntRegister()
    {
        var registry = new ActorProcessRegistry();

        var name = new Uri("trupe://localhost/456");
        var @ref = Substitute.For<IActorReference>();
        @ref.Name.Returns(name);

        await Assert
            .That(() => registry.GetProcess(@ref))
            .Throws<ActorProcessNotRegisterException>();
    }
}
