using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Trupe.Abstractions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Pipelines;
using Trupe.Abstractions.Pipelines.Metadatas;
using Trupe.Messages;
using Trupe.Pipelines.Middlewares;

namespace Trupe.Tests.Pipelines.Middlewares;

public class ActorProcessDispatcherMiddlewareTest
{
    [Test]
    public async Task InvokeAsync_Should_CallMailboxEnqueue()
    {
        var middleware = new ActorProcessDispatcherMiddleware();

        var mailbox = Substitute.For<IMailbox>();
        var process = Substitute.For<IActorProcess>();
        process.Mailbox.Returns(mailbox);

        var context = Substitute.For<ISendPipelineContext>();
        context.Metadata.Returns(
            new PipelineMetadataCollection([new ActorProcessMetadata(process)])
        );

        var message = new TellMessage(new object(), [], CancellationToken.None);
        context.Message.Returns(message);

        context.CancellationToken.Returns(CancellationToken.None);

        await Assert
            .That(async () => middleware.InvokeAsync(context, _ => new ValueTask()))
            .ThrowsNothing();

        await mailbox.Received(1).EnqueueAsync(message, Arg.Any<CancellationToken>());
    }
}
