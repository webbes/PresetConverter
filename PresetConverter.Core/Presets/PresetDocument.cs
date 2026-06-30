namespace PresetConverter;

public abstract record PresetDocument(
    PresetKind Kind,
    string Name,
    string SourceName,
    string? SlicerFlavor)
{
    public string PresetId { get; init; } = Name;
}
