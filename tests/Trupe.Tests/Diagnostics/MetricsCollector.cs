using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Trupe.Tests.Diagnostics;

public sealed record Measurement(string Name, long Value, KeyValuePair<string, object?>[] Tags);

public sealed class MetricsCollector : IDisposable
{
    private readonly MeterListener _listener;

    public List<Measurement> Measurements { get; } = [];

    public MetricsCollector()
    {
        _listener = new MeterListener();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TrupeDiagnostics.InstrumentationName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) =>
            Measurements.Add(new Measurement(instrument.Name, measurement, tags.ToArray())));

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            Measurements.Add(new Measurement(instrument.Name, measurement, tags.ToArray())));

        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();
}
