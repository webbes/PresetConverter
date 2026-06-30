namespace PresetConverter;

public sealed record ConversionWarning(string Code, string Message, string? SourceName = null);