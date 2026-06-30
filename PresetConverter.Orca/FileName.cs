namespace PresetConverter;

internal static class FileName
{
    public static string Safe(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Trim();
    }
}
