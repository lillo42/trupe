
using Trupe.ActorReferences;
using Trupe.Mailboxes;
using Trupe.Tests.Actors;
using Trupe.Tests.Messages;

namespace Trupe.Tests;

public class LocalActorReferenceTest
{
    [Test]
    [Timeout(10_000)]
    public void Tell_With_Object(CancellationToken cancellationToken)
    {
        var reference = new LocalActorReference(
            new Uri("localhost:test_with_obj"), 
            new SimpleActor(), 
            new ChannelMailBox());
        
        reference.Start();
        
        var message = new SimpleMessage();
        reference.Tell(message);

        while (!message.Received && !cancellationToken.IsCancellationRequested)
        {
            Task.Delay(100, cancellationToken).Wait(cancellationToken);
        }
        
        reference.Stop();
    }
    
    [Test]
    [Timeout(10_000)]
    public void Tell_With_TypedMessage(CancellationToken cancellationToken)
    {
        var reference = new LocalActorReference(
            new Uri("localhost:test_with_obj"), 
            new SimpleTypedActor(), 
            new ChannelMailBox());
        
        reference.Start();
        
        var message = new SimpleMessage();
        reference.Tell(message);

        while (!message.Received && !cancellationToken.IsCancellationRequested)
        {
            Task.Delay(100, cancellationToken).Wait(cancellationToken);
        }
        
        reference.Stop();
    }
}
