using System;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Specifies which pipelines a middleware participates in.
/// </summary>
[Flags]
public enum MiddlewareScope
{
    /// <summary>
    /// The middleware does not participate in any pipeline.
    /// </summary>
    None = 0,

    /// <summary>
    /// The middleware participates in the outgoing (send) pipeline.
    /// </summary>
    Send = 1,

    /// <summary>
    /// The middleware participates in the incoming (receive) pipeline.
    /// </summary>
    Receive = 2,

    /// <summary>
    /// The middleware participates in both the send and receive pipelines.
    /// </summary>
    Both = Send | Receive,
}
