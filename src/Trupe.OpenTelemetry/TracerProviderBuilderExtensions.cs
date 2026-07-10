using OpenTelemetry.Trace;

namespace Trupe.OpenTelemetry;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> to register Trupe tracing instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds Trupe distributed tracing instrumentation to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Registers the Trupe <see cref="System.Diagnostics.ActivitySource"/> so that actor message
    /// processing spans are captured and exported by the configured OpenTelemetry pipeline.
    /// </remarks>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenTelemetry()
    ///     .WithTracing(b => b.AddTrupeInstrumentation());
    /// </code>
    /// </example>
    public static TracerProviderBuilder AddTrupeInstrumentation(this TracerProviderBuilder builder)
    {
        return builder.AddSource(TrupeDiagnostics.InstrumentationName);
    }
}