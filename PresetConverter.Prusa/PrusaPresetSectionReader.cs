using System.Text.RegularExpressions;

namespace PresetConverter;

internal static partial class PrusaPresetSectionReader
{
    public static IEnumerable<PrusaPresetSection> ReadSections(TextReader reader, string sourceName)
    {
        string? slicerFlavor = null;
        string? currentRawKind = null;
        PresetKind? currentKind = null;
        string? currentName = null;
        var rootSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        var currentSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        var sawBundleSection = false;
        var templateProfile = false;

        foreach (var rawLine in ReadLines(reader))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var generatedMatch = GeneratedByRegex().Match(line);
            if (generatedMatch.Success)
            {
                slicerFlavor = generatedMatch.Groups[1].Value;
                continue;
            }

            var sectionMatch = BundleSectionRegex().Match(line);
            if (sectionMatch.Success)
            {
                if (currentKind is not null && currentRawKind is not null && currentName is not null)
                {
                    yield return new PrusaPresetSection(currentKind.Value, currentRawKind, currentName, sourceName, slicerFlavor, templateProfile, currentSettings);
                }

                sawBundleSection = true;
                currentRawKind = sectionMatch.Groups["kind"].Value.Trim();
                currentKind = ParseKind(currentRawKind);
                currentName = sectionMatch.Groups["name"].Value.Trim();
                currentSettings = new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }

            if (line.StartsWith('#') || line.StartsWith(';') || line.StartsWith('['))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if (!sawBundleSection && key.Equals("templates_profile", StringComparison.Ordinal) && value == "1")
            {
                templateProfile = true;
            }

            if (sawBundleSection)
            {
                currentSettings[key] = value;
            }
            else
            {
                rootSettings[key] = value;
            }
        }

        if (currentKind is not null && currentRawKind is not null && currentName is not null)
        {
            yield return new PrusaPresetSection(currentKind.Value, currentRawKind, currentName, sourceName, slicerFlavor, templateProfile, currentSettings);
        }
        else if (!sawBundleSection && rootSettings.Count > 0)
        {
            var kind = PrusaPresetKindDetector.Detect(rootSettings, null) ?? PresetKind.Filament;
            yield return new PrusaPresetSection(kind, kind.ToString(), string.Empty, sourceName, slicerFlavor, templateProfile, rootSettings);
        }
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static PresetKind? ParseKind(string rawKind) => rawKind.ToLowerInvariant() switch
    {
        "filament" => PresetKind.Filament,
        "print" => PresetKind.Print,
        "printer" => PresetKind.Printer,
        "physical_printer" => PresetKind.PhysicalPrinter,
        _ => null
    };

    [GeneratedRegex(@"^#\s*generated\s+by\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex GeneratedByRegex();

    [GeneratedRegex(@"^\[(?<kind>[\w\s+\-]+):(?<name>[^\]]+)\]$")]
    private static partial Regex BundleSectionRegex();
}
