namespace PresetConverter;

internal static class PrusaPresetInheritanceResolver
{
    public static IReadOnlyDictionary<string, string> Flatten(
        PrusaPresetSection section,
        IReadOnlyDictionary<(PresetKind Kind, string Name), PrusaPresetSection> sections)
    {
        return Flatten(section, sections, []);
    }

    private static Dictionary<string, string> Flatten(
        PrusaPresetSection section,
        IReadOnlyDictionary<(PresetKind Kind, string Name), PrusaPresetSection> sections,
        HashSet<(PresetKind Kind, string Name)> stack)
    {
        var key = (section.Kind, section.Name);
        if (!stack.Add(key))
        {
            throw new InvalidOperationException($"Cyclic inherits chain detected at {section.RawKind}:{section.Name}.");
        }

        try
        {
            var flattened = new Dictionary<string, string>(StringComparer.Ordinal);
            if (section.Settings.TryGetValue("inherits", out var parentNames) && !string.IsNullOrWhiteSpace(parentNames))
            {
                foreach (var parentName in SplitPresetReferences(parentNames))
                {
                    if (!sections.TryGetValue((section.Kind, parentName), out var parent))
                    {
                        throw new InvalidOperationException($"Inherited parent preset '{parentName}' was not found in the bundle.");
                    }

                    foreach (var (parentKey, parentValue) in Flatten(parent, sections, stack))
                    {
                        flattened[parentKey] = parentValue;
                    }
                }
            }

            foreach (var (childKey, childValue) in section.Settings)
            {
                flattened[childKey] = childValue;
            }

            return flattened;
        }
        finally
        {
            stack.Remove(key);
        }
    }

    private static IEnumerable<string> SplitPresetReferences(string value)
    {
        foreach (var reference in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizePresetReference(reference);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static string NormalizePresetReference(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? System.Text.RegularExpressions.Regex.Unescape(value[1..^1])
            : value;
    }
}