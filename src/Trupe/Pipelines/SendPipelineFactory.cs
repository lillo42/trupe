using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

/// <summary>
/// Factory that creates send pipelines by resolving middleware instances from the service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve middleware instances.</param>
/// <param name="lookup">The pipeline lookup used to resolve registered middlewares.</param>
public class SendPipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
    : AbstractPipelineFactory(provider, lookup),
        ISendPipelineFactory
{
    /// <inheritdoc />
    protected override MiddlewareScope Scope => MiddlewareScope.Send;

    /// <summary>
    /// Creates a new send pipeline for the specified actor and message type.
    /// </summary>
    /// <param name="actorType">The type of the target actor.</param>
    /// <param name="messageType">The type of the message being sent.</param>
    /// <returns>A new <see cref="ISendPipeline"/> with resolved middleware instances.</returns>
    public ISendPipeline Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType
    )
    {
        var types = GetMiddlewareTypes(actorType, messageType);

        return new SendPipeline(
            types
                .Select(type => (ISendMiddleware)ServiceProvider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
