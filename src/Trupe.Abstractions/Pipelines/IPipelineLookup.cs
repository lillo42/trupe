using System;
using System.Collections.Generic;

namespace Trupe.Abstractions.Pipelines;

public interface IPipelineLookup
{
    IEnumerable<IMiddlewareConfiguration> GetMiddlewares(Type actorType, Type messageType);
}
