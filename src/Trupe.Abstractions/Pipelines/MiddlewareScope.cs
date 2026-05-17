using System;

namespace Trupe.Abstractions.Pipelines;

[Flags]
public enum MiddlewareScope
{
    Both = Send | Receive,
    Send = 1,
    Receive = 2,
}
