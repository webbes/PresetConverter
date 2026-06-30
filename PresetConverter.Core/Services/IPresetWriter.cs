namespace PresetConverter;

public interface IPresetWriter
{
    IEnumerable<ConversionItemResult> WritePresets(IEnumerable<PresetDocument> presets, PresetWriteRequest request);
}