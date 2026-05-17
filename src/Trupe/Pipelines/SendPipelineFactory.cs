using System;
using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<(Type, Type), ImmutableList<Type>> _cache = [];

    public ISendPipeline Create(Type actorType, Type messageType)
    {
        var types = _cache.GetOrAdd(
            (actorType, messageType),
            val => GetMiddlewareTypes(val.Item1, val.Item2)
        );

        return new SendPipeline(
            types
                .Select(type => (ISendMiddleware)provider.GetRequiredService(type))
                .ToImmutableList()
        );
    }
}
