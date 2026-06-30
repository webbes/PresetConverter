namespace PresetConverter;

public sealed record PrinterPreset(
    string Name,
    string SourceName,
    string? SlicerFlavor) : PresetDocument(PresetKind.Printer, Name, SourceName, SlicerFlavor);