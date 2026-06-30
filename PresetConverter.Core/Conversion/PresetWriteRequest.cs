namespace PresetConverter;

public sealed class PresetWriteRequest
{
    public required string OutputDirectory { get; set; }

    public bool ForceOutputDirectory { get; set; }

    public ExistingFileMode ExistingFileMode { get; set; } = ExistingFileMode.Skip;
}