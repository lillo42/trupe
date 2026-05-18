using System.Threading.Tasks;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// Represents a function that invokes the next middleware in a generic pipeline.
/// </summary>
/// <param name="context">The pipeline context to pass to the next middleware.</param>
/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
public delegate ValueTask NextDelegate(IPipelineContext context);
