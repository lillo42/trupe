using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class SendPipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
    : AbstractPipelineFactory(provider, lookup),
        ISendPipelineFactory
{
    protected override MiddlewareScope Scope => MiddlewareScope.Send;

    public ISendPipeline Create(Type actorType, Type messageType)
    {
        var types = GetMiddlewareTypes(actorType, messageType);

        return new SendPipeline(
            types
                .Select(type => (ISendMiddleware)provider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
