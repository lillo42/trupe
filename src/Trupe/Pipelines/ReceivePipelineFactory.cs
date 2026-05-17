using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Pipelines;

public class ReceivePipelineFactory(IServiceProvider provider, IPipelineLookup lookup)
    : AbstractPipelineFactory(provider, lookup),
        IReceivePipelineFactory
{
    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<Type>> _cache = [];

    protected override MiddlewareScope Scope => MiddlewareScope.Receive;

    public IReceivePipeline Create(Type actorType, Type messageType)
    {
        var types = _cache.GetOrAdd(
            (actorType, messageType),
            val => GetMiddlewareTypes(val.Item1, val.Item2)
        );

        return new ReceivePipeline(
            types
                .Select(type => (IReceiveMiddleware)provider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
