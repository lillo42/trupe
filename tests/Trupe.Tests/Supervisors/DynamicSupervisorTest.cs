using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Trupe.ActorReferences;
using Trupe.Factories;
using Trupe.Mailboxes;
using Trupe.Supervisors;

namespace Trupe.Tests.Supervisors;

public class DynamicSupervisorTest
{
    [Test]
    public async Task ChildrenCount_Should_BeCorrectlyAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert
                .That(supervisor.Children)
                .Count()
                .IsEqualTo(RuntimeFeature.IsDynamicCodeSupported ? 4 : 2);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Strategy_Should_BeOneForOne(CancellationToken cancellationToken)
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        // Assert - DynamicSupervisor always uses OneForOne strategy
        await Assert.That(supervisor.GetStrategy()).IsEqualTo(Strategy.OneForOne);
    }

    [Test]
    public async Task AddChild_Should_AddActorAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Add a new child after initialization
            supervisor.AddNewChild<SimpleUntypedActor>();

            // Ensure the new child is added
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount + 1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task AddChildAsync_Should_AddActorAfterInitialization(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Add a new child after initialization using async method
            await supervisor.AddNewChildAsync<SimpleUntypedActor>(cancellationToken);

            // Ensure the new child is added
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount + 1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_ProcessMessagesCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert
            foreach (var child in supervisor.Children)
            {
                var response = await child.AskAsync<SetMessage, string>(
                    new SetMessage("hello"),
                    cancellationToken: cancellationToken
                );

                await Assert.That(response).IsEqualTo("HELLO");
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task Children_Should_RestartWithOneForOneStrategy_When_ActorThrowException(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());

        var mailbox = new ChannelMailbox();
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act & Assert - DynamicSupervisor always uses OneForOne strategy
            foreach (var child in supervisor.Children)
            {
                var message = Uuid.NewUuid().ToString();
                await child.AskAsync<SetMessage, string>(
                    new SetMessage(message),
                    cancellationToken: cancellationToken
                );

                await Assert.ThrowsAsync<Exception>(async () =>
                    await child.AskAsync<RaiseException, object>(
                        new RaiseException(),
                        cancellationToken: cancellationToken
                    )
                );

                var response = await child.AskAsync<GetState, string>(
                    new GetState(),
                    cancellationToken: cancellationToken
                );
                await Assert.That(response).IsEqualTo(string.Empty);
            }
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task DynamicallyAddedChild_Should_ProcessMessages(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);

            // Ensure all children are initialized
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Act - Add a new child after initialization
            var newChild = supervisor.AddNewChild<SimpleUntypedActor>();

            // Ensure the new child is added and started
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert - the new child should be able to process messages
            var response = await newChild.AskAsync<SetMessage, string>(
                new SetMessage("dynamic"),
                cancellationToken: cancellationToken
            );

            await Assert.That(response).IsEqualTo("DYNAMIC");
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task RemoveChild_Should_RemoveActorFromChildren(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Remove a child by reference
            supervisor.RemoveExistingChild(supervisor.Children.First());
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount - 1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task RemoveChildAsync_Should_RemoveActorFromChildren(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Remove a child by reference using async method
            await supervisor.RemoveExistingChildAsync(
                supervisor.Children.First(),
                cancellationToken
            );
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount - 1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task RemoveChild_Should_DoNothing_When_ReferenceNotFound(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Try to remove a non-existent reference
            var unknownRef = new LocalActorReference(new ChannelMailbox());
            supervisor.RemoveExistingChild(unknownRef);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert - children count should remain unchanged
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task RemoveChildAsync_Should_DoNothing_When_ReferenceNotFound(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new SimpleDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Try to remove a non-existent reference using async method
            var unknownRef = new LocalActorReference(new ChannelMailbox());
            await supervisor.RemoveExistingChildAsync(unknownRef, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            // Assert - children count should remain unchanged
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    [Test]
    public async Task HandleActorTerminated_Should_RemoveActor_When_RestartPolicyIsTransient(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var mailbox = new ChannelMailbox();

        var supervisor = new TransientDynamicSupervisor(new ActorFactory());
        supervisor.Context = new ActorContext(new LocalActorReference(mailbox));

        var process = new ActorProcess(supervisor, mailbox);
        process.Start();

        try
        {
            await supervisor.InitializeAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            var initialCount = RuntimeFeature.IsDynamicCodeSupported ? 4 : 2;
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount);

            // Act - Cause an actor to fail, which triggers termination under Transient policy
            var firstChild = supervisor.Children.First();
            await Assert.ThrowsAsync<Exception>(async () =>
                await firstChild.AskAsync<RaiseException, object>(
                    new RaiseException(),
                    cancellationToken: cancellationToken
                )
            );

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

            // Assert - The terminated actor should be removed under Transient policy
            await Assert.That(supervisor.Children).Count().IsEqualTo(initialCount - 1);
        }
        finally
        {
            await process.StopAsync();
        }
    }

    public class SimpleDynamicSupervisor(IActorFactory actorFactory)
        : DynamicSupervisor(actorFactory, new NullLogger<SimpleDynamicSupervisor>())
    {
        public Strategy GetStrategy() => Strategy;

        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                await AddChildAsync<TypedActor>(cancellationToken);
                await AddChildAsync(typeof(TypedActor), cancellationToken);
            }

            await AddChildAsync<SimpleUntypedActor>(cancellationToken);
            await AddChildAsync(typeof(SimpleUntypedActor), cancellationToken);
        }

        public IActorReference AddNewChild<TActor>()
            where TActor : IActor
        {
            return AddChild<TActor>();
        }

        public ValueTask<IActorReference> AddNewChildAsync<TActor>(
            CancellationToken cancellationToken = default
        )
            where TActor : IActor
        {
            return AddChildAsync<TActor>(cancellationToken);
        }

        public void RemoveExistingChild(IActorReference reference)
        {
            RemoveActor(reference);
        }

        public ValueTask RemoveExistingChildAsync(
            IActorReference reference,
            CancellationToken cancellationToken = default
        )
        {
            return RemoveActorAsync(reference, cancellationToken);
        }
    }

    public class TransientDynamicSupervisor(IActorFactory actorFactory)
        : DynamicSupervisor(actorFactory, new NullLogger<TransientDynamicSupervisor>())
    {
        protected override RestartPolicy Restart => RestartPolicy.Transient;

        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                await AddChildAsync<TypedActor>(cancellationToken);
                await AddChildAsync(typeof(TypedActor), cancellationToken);
            }

            await AddChildAsync<SimpleUntypedActor>(cancellationToken);
            await AddChildAsync(typeof(SimpleUntypedActor), cancellationToken);
        }
    }

    public class TypedActor
        : Actor,
            IHandleActorMessage<SetMessage>,
            IHandleActorMessage<GetState>,
            IHandleActorMessage<RaiseException>
    {
        private string _state = string.Empty;

        public ValueTask HandleAsync(
            SetMessage message,
            CancellationToken cancellationToken = default
        )
        {
            _state = message.Content.ToUpper();
            Context.Response = _state;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(
            GetState message,
            CancellationToken cancellationToken = default
        )
        {
            Context.Response = _state;
            return ValueTask.CompletedTask;
        }

        public ValueTask HandleAsync(
            RaiseException message,
            CancellationToken cancellationToken = default
        )
        {
            throw new Exception();
        }
    }

    public class SimpleUntypedActor : Actor
    {
        private string _state = string.Empty;

        public override ValueTask HandleAsync(
            object? message,
            CancellationToken cancellationToken = default
        )
        {
            if (message is SetMessage simpleMessage)
            {
                _state = simpleMessage.Content.ToUpper();
                Context.Response = _state;
            }
            else if (message is GetState)
            {
                Context.Response = _state;
            }
            else if (message is RaiseException)
            {
                throw new Exception();
            }

            return ValueTask.CompletedTask;
        }
    }

    public record SetMessage(string Content);

    public record GetState();

    public record RaiseException();

    public class ActorFactory : IActorFactory
    {
        public IActor CreateActor(Type actorType)
        {
            return (IActor)Activator.CreateInstance(actorType)!;
        }
    }
}
