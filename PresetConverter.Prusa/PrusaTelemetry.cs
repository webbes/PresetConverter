using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PresetConverter;

public static class PrusaTelemetry
{
    public const string Name = "PresetConverter.Prusa";

    public static readonly ActivitySource ActivitySource = new(Name);

    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> PresetsRead = Meter.CreateCounter<long>(
        "prusa.presets.read",
        description: "Number of Prusa filament presets read.");

    public static readonly Counter<long> PresetsSkipped = Meter.CreateCounter<long>(
        "prusa.presets.skipped",
        description: "Number of Prusa presets skipped.");

    public static readonly Histogram<double> ReadDuration = Meter.CreateHistogram<double>(
        "prusa.read.duration",
        unit: "ms",
        description: "Time spent reading Prusa presets.");
}
