using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class ReceivePipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
    : AbstractPipelineFactory(provider, lookup),
        IReceivePipelineFactory
{
    protected override MiddlewareScope Scope => MiddlewareScope.Receive;

    public IReceivePipeline Create(Type actorType, Type messageType)
    {
        var types = GetMiddlewareTypes(actorType, messageType);

        return new ReceivePipeline(
            types
                .Select(type => (IReceiveMiddleware)provider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
