using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;
using Trupe.Extensions;

namespace Trupe.Pipelines;

public class PipelineRegistry(IOptions<PipelineOptions> options) : IPipelineLookup
{
    public IEnumerable<IMiddlewareConfiguration> GetMiddlewares(Type actorType, Type messageType)
    {
        return options
            .Value.Middlewares.Where(mw =>
                (mw.ActorType == null || mw.ActorType.IsAssignableFrom(actorType))
                && (mw.MessageType == null || mw.MessageType.IsAssignableFrom(messageType))
            )
            .Select(mw =>
            {
                var scope = MiddlewareScope.None;
                if (mw.MiddlewareType.IReceiveMiddleware())
                {
                    scope |= MiddlewareScope.Receive;
                }

                if (mw.MiddlewareType.IsSendMiddleware())
                {
                    scope |= MiddlewareScope.Send;
                }

                return new MiddlewareConfiguration(mw.Order, mw.Metadata, mw.MiddlewareType)
                {
                    Scope = scope,
                };
            });
    }
}
