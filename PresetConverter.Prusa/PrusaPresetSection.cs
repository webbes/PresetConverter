namespace PresetConverter;

internal sealed record PrusaPresetSection(
    PresetKind Kind,
    string RawKind,
    string Name,
    string SourceName,
    string? SlicerFlavor,
    bool IsTemplateProfile,
    IReadOnlyDictionary<string, string> Settings);
