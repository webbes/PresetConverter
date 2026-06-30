namespace PresetConverter;

public sealed class ConversionItemResult
{
    private readonly List<ConversionWarning> _warnings = [];

    private ConversionItemResult(ConversionItemStatus status, string sourceName, string? outputPath = null)
    {
        Status = status;
        SourceName = sourceName;
        OutputPath = outputPath;
    }

    public ConversionItemStatus Status { get; }

    public string SourceName { get; }

    public string? OutputPath { get; }

    public Exception? Exception { get; private init; }

    public IReadOnlyList<ConversionWarning> Warnings => _warnings;

    public static ConversionItemResult Succeeded(string sourceName, string outputPath) => new(ConversionItemStatus.Succeeded, sourceName, outputPath);

    public static ConversionItemResult Skipped(string sourceName, ConversionWarning warning)
    {
        var result = new ConversionItemResult(ConversionItemStatus.Skipped, sourceName);
        result._warnings.Add(warning);
        return result;
    }

    public static ConversionItemResult Failed(string sourceName, Exception exception) =>
        new(ConversionItemStatus.Failed, sourceName) { Exception = exception };
}