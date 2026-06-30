namespace PresetConverter;

public sealed record PrintPreset(
    string Name,
    string SourceName,
    string? SlicerFlavor) : PresetDocument(PresetKind.Print, Name, SourceName, SlicerFlavor);