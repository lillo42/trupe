using OpenTelemetry.Metrics;

namespace Trupe.OpenTelemetry;

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> to register Trupe metrics instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds Trupe metrics instrumentation to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Registers the Trupe <see cref="System.Diagnostics.Metrics.Meter"/> so that actor system
    /// metrics — including mailbox queue lengths, message processing durations, supervisor restarts,
    /// and dead letter counts — are captured and exported by the configured OpenTelemetry pipeline.
    /// </remarks>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenTelemetry()
    ///     .WithMetrics(b => b.AddTrupeInstrumentation());
    /// </code>
    /// </example>
    public static MeterProviderBuilder AddTrupeInstrumentation(this MeterProviderBuilder builder)
    {
        return builder.AddMeter(TrupeDiagnostics.InstrumentationName);
    }
}