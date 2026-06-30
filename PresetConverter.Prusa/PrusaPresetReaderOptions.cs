namespace PresetConverter;

public sealed class PrusaPresetReaderOptions
{
    public string ConfigurationDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrusaSlicer");

    public string[] InputPatterns { get; set; } = [];

    public bool SkipIncompletePresets { get; set; } = true;

    public string PresetIdPattern { get; set; } = "^[^@]+$";

    public static string[] CreateDefaultInputPatterns() =>
    [
        Path.Combine("filament", "*.ini"),
        Path.Combine("vendor", "*.ini")
    ];
}
