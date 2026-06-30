namespace PresetConverter;

public interface IPresetReader
{
    IEnumerable<PresetDocument> ReadPresets(Stream stream, string sourceName);
}