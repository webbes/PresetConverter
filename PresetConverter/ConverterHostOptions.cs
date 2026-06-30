namespace PresetConverter;

internal sealed class ConverterHostOptions
{
    public List<string> Inputs { get; set; } = [];

    public string? OutputDirectory { get; set; }

    public ExistingFileMode OnExisting { get; set; } = ExistingFileMode.Skip;

    public bool ForceOutputDirectory { get; set; } = true;
}
