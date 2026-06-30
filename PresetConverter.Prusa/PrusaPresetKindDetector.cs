namespace PresetConverter;

internal static class PrusaPresetKindDetector
{
    private static readonly HashSet<string> FilamentKeys = new(StringComparer.Ordinal)
    {
        "filament_type", "filament_colour", "filament_diameter", "temperature", "bed_temperature", "first_layer_temperature"
    };

    private static readonly HashSet<string> PrintKeys = new(StringComparer.Ordinal)
    {
        "layer_height", "perimeters", "fill_density", "support_material", "travel_speed", "infill_speed"
    };

    private static readonly HashSet<string> PrinterKeys = new(StringComparer.Ordinal)
    {
        "nozzle_diameter", "bed_shape", "start_gcode", "gcode_flavor", "machine_max_feedrate_x", "printer_technology"
    };

    public static PresetKind? Detect(IReadOnlyDictionary<string, string> settings, PresetKind? explicitKind)
    {
        if (explicitKind is not null)
        {
            return explicitKind;
        }

        if (settings.TryGetValue("ini_type", out var iniType) && Enum.TryParse<PresetKind>(iniType.Replace("_", "", StringComparison.Ordinal), true, out var detected))
        {
            return detected;
        }

        var scores = new[]
        {
            (Kind: PresetKind.Filament, Count: settings.Keys.Count(FilamentKeys.Contains)),
            (Kind: PresetKind.Print, Count: settings.Keys.Count(PrintKeys.Contains)),
            (Kind: PresetKind.Printer, Count: settings.Keys.Count(PrinterKeys.Contains))
        };

        var best = scores.OrderByDescending(score => score.Count).First();
        return best.Count >= 2 ? best.Kind : null;
    }
}