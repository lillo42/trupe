using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Trupe;

/// <summary>
/// Central diagnostics hub for the Trupe actor system, exposing the
/// <see cref="System.Diagnostics.ActivitySource"/> and <see cref="System.Diagnostics.Metrics.Meter"/>
/// used to emit distributed traces and metrics across the runtime.
/// </summary>
/// <remarks>
/// To collect Trupe telemetry, register both instruments with your OpenTelemetry pipeline
/// via the <c>Trupe.OpenTelemetry</c> package:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(b => b.AddTrupeInstrumentation())
///     .WithMetrics(b => b.AddTrupeInstrumentation());
/// </code>
/// </remarks>
public static class TrupeDiagnostics
{
    /// <summary>
    /// The instrumentation name used to identify all Trupe telemetry.
    /// Pass this value to <see cref="ActivitySource"/> and <see cref="Meter"/> registrations
    /// when configuring OpenTelemetry manually.
    /// </summary>
    public const string InstrumentationName = "Trupe";

    /// <summary>
    /// The version of the Trupe instrumentation, read from the assembly's informational version
    /// as set by MinVer at build time.
    /// </summary>
    private static readonly string Version =
        typeof(TrupeDiagnostics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> used to create distributed tracing spans
    /// for actor message processing operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(InstrumentationName, Version);

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> used to record actor system metrics,
    /// including mailbox queue lengths, message processing durations, supervisor restart counts,
    /// and dead letter counts.
    /// </summary>
    public static readonly Meter Meter = new(InstrumentationName, Version);
}