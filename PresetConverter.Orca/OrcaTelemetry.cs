using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PresetConverter;

public static class OrcaTelemetry
{
    public const string Name = "PresetConverter.Orca";

    public static readonly ActivitySource ActivitySource = new(Name);

    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> PresetsWritten = Meter.CreateCounter<long>(
        "orca.presets.written",
        description: "Number of Orca presets written.");

    public static readonly Counter<long> PresetsSkipped = Meter.CreateCounter<long>(
        "orca.presets.skipped",
        description: "Number of Orca presets skipped.");

    public static readonly Counter<long> PresetsFailed = Meter.CreateCounter<long>(
        "orca.presets.failed",
        description: "Number of Orca presets that failed to write.");

    public static readonly Histogram<double> WriteDuration = Meter.CreateHistogram<double>(
        "orca.write.duration",
        unit: "ms",
        description: "Time spent writing Orca presets.");
}
