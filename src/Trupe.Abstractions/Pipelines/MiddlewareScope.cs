using System;

namespace Trupe.Abstractions.Pipelines;

[Flags]
public enum MiddlewareScope
{
    None = 0,
    Send = 1,
    Receive = 2,
    Both = Send | Receive,
}
