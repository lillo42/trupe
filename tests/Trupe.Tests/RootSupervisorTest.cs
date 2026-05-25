using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Supervisors;
using Trupe.Supervisors.Commands;

namespace Trupe.Tests;

public class RootSupervisorTest
{
    [Test]
    public async Task InitializeAsync()
    {
        // Options
        var opt = new RootSupervisorOptions();
        opt.Children.Add(new ChildSpecification { ActorType = typeof(SomeActor) });
        opt.Children.Add(new ChildSpecification { ActorType = typeof(OtherActor) });

        var options = Substitute.For<IOptions<RootSupervisorOptions>>();
        options.Value.Returns(opt);

        var logger = new NullLogger<RootSupervisor>();

        // Creating root Supervisors
        var root = new RootSupervisor(options, logger);

        // Initializing ActorContext
        // Service Provider
        var serviceProvider = Substitute.For<IServiceProvider>();

        // Actor Factory
        var actorFactory = Substitute.For<IActorFactory>();
        actorFactory.CreateActor(typeof(SomeActor)).Returns(new SomeActor());
        actorFactory.CreateActor(typeof(OtherActor)).Returns(new OtherActor());
        serviceProvider.GetService(typeof(IActorFactory)).Returns(actorFactory);

        // Actor Reference
        var referenceFactory = Substitute.For<IActorReferenceFactory>();
        var actorReference = Substitute.For<IActorReference>();
        serviceProvider.GetService(typeof(IActorReferenceFactory)).Returns(referenceFactory);
        referenceFactory
            .Create(Arg.Any<string>(), Arg.Any<IActorProcess>())
            .Returns(actorReference);

        // Actor Process registry
        var registry = Substitute.For<IActorProcessRegistry>();
        registry.GetReference(Arg.Any<Uri>()).Returns(actorReference);
        serviceProvider.GetService(typeof(IActorProcessRegistry)).Returns(registry);

        // Service Provder Scope
        var scope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(scope);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        var context = Substitute.For<IActorContext>();
        context.ServiceProvider.Returns(serviceProvider);

        var self = Substitute.For<IActorReference>();
        context.Self.Returns(self);
        root.Context = context;

        await root.InitializeAsync(CancellationToken.None);

        actorFactory.Received(1).CreateActor(typeof(SomeActor));
        actorFactory.Received(1).CreateActor(typeof(OtherActor));

        self.Received(1)
            .Tell(
                Arg.Is<object>(x =>
                    x is AddActor && ((AddActor)x).Child.ActorType == typeof(SomeActor)
                )
            );
        self.Received(1)
            .Tell(
                Arg.Is<object>(x =>
                    x is AddActor && ((AddActor)x).Child.ActorType == typeof(OtherActor)
                )
            );
    }

    public class SomeActor : Actor { }

    public class OtherActor : Actor { }
}
