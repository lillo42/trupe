using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Factory that creates receive pipelines by resolving middleware instances from the service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve middleware instances.</param>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public class ReceivePipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
    : AbstractPipelineFactory(provider, lookup),
        IReceivePipelineFactory
{
    /// <inheritdoc />
    protected override MiddlewareScope Scope => MiddlewareScope.Receive;

    /// <summary>
    /// Creates a new receive pipeline for the specified actor and message type.
    /// </summary>
    /// <param name="actorType">The type of the actor processing the message.</param>
    /// <param name="messageType">The type of the message being processed.</param>
    /// <returns>A new <see cref="IReceivePipeline"/> with resolved middleware instances.</returns>
    public IReceivePipeline Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType)
    {
        var types = GetMiddlewareTypes(actorType, messageType);

        return new ReceivePipeline(
            types
                .Select(type => (IReceiveMiddleware)provider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
