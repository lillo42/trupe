using System;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;

namespace Trupe.Tests;

public class ActorFactoryTest
{
    [Test]
    public async Task CreateActor()
    {
        var expectedActor = new SomeActor();
        var provider = Substitute.For<IServiceProvider>();

        provider.GetService(typeof(SomeActor)).Returns(expectedActor);

        var factory = new ActorFactory(provider);
        var actor = factory.CreateActor(typeof(SomeActor));

        await Assert.That(actor).IsEqualTo(expectedActor);
    }

    public class SomeActor : Actor { }
}
