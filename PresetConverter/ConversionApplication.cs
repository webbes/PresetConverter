namespace PresetConverter;

internal sealed class ConversionApplication(
    IPresetReader presetReader,
    IPresetWriter presetWriter,
    FilamentPresetSelector presetSelector,
    Microsoft.Extensions.Options.IOptions<PrusaPresetReaderOptions> prusaOptions)
{
    public IReadOnlyList<ConversionItemResult> Run(ConverterHostOptions options)
    {
        var outputDirectory = ResolveOutputDirectory(options.OutputDirectory);
        var writeRequest = new PresetWriteRequest
        {
            OutputDirectory = outputDirectory,
            ForceOutputDirectory = options.ForceOutputDirectory,
            ExistingFileMode = options.OnExisting
        };

        var presets = new List<PresetDocument>();
        var inputExpressions = ResolveInputExpressions(options);
        foreach (var input in ExpandInputs(inputExpressions))
        {
            using var stream = File.OpenRead(input);
            presets.AddRange(presetReader.ReadPresets(stream, input));
        }

        var selectedPresets = presetSelector.Select(presets);
        return presetWriter.WritePresets(selectedPresets, writeRequest).ToArray();
    }

    private IEnumerable<string> ResolveInputExpressions(ConverterHostOptions options)
    {
        if (options.Inputs.Count > 0)
        {
            return options.Inputs;
        }

        return ResolvePrusaInputs();
    }

    private List<string> ResolvePrusaInputs()
    {
        var prusa = prusaOptions.Value;
        if (string.IsNullOrWhiteSpace(prusa.ConfigurationDirectory) || prusa.InputPatterns.Length == 0)
        {
            throw new ArgumentException("Provide at least one .ini file, directory, or wildcard with --input or PresetConverter:Inputs in appsettings.json, or configure Prusa:ConfigurationDirectory and Prusa:InputPatterns.");
        }

        var configurationDirectory = Environment.ExpandEnvironmentVariables(prusa.ConfigurationDirectory);
        var inputs = new List<string>();
        foreach (var pattern in prusa.InputPatterns)
        {
            var expandedPattern = Environment.ExpandEnvironmentVariables(pattern);
            var input = Path.IsPathRooted(expandedPattern)
                ? expandedPattern
                : Path.Combine(configurationDirectory, expandedPattern);
            if (CanExpandInput(input))
            {
                inputs.Add(input);
            }
        }

        if (inputs.Count == 0)
        {
            throw new DirectoryNotFoundException($"No Prusa preset inputs were found in '{configurationDirectory}'. Configure --input or --prusa-config-dir if your PrusaSlicer configuration files are stored somewhere else.");
        }

        return inputs;
    }

    private static bool CanExpandInput(string input)
    {
        if (Directory.Exists(input) || File.Exists(input))
        {
            return true;
        }

        var pattern = Path.GetFileName(input);
        if (!string.IsNullOrEmpty(pattern) && pattern.Contains('*', StringComparison.Ordinal))
        {
            var directory = Path.GetDirectoryName(input);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
        }

        return false;
    }

    private static string ResolveOutputDirectory(string? outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Path.GetFullPath(outputDirectory);
        }

        return Path.Combine(AppContext.BaseDirectory, "Output");
    }

    private static IEnumerable<string> ExpandInputs(IEnumerable<string> inputs)
    {
        foreach (var input in inputs)
        {
            var fullInput = Environment.ExpandEnvironmentVariables(input);
            if (Directory.Exists(fullInput))
            {
                foreach (var file in Directory.EnumerateFiles(fullInput, "*.ini"))
                {
                    yield return Path.GetFullPath(file);
                }

                continue;
            }

            var directory = Path.GetDirectoryName(fullInput);
            var pattern = Path.GetFileName(fullInput);
            if (!string.IsNullOrEmpty(pattern) && pattern.Contains('*', StringComparison.Ordinal))
            {
                directory = string.IsNullOrWhiteSpace(directory) ? Directory.GetCurrentDirectory() : directory;
                foreach (var file in Directory.EnumerateFiles(directory, pattern))
                {
                    yield return Path.GetFullPath(file);
                }

                continue;
            }

            if (!File.Exists(fullInput))
            {
                throw new FileNotFoundException($"Input file not found: {input}");
            }

            yield return Path.GetFullPath(fullInput);
        }
    }
}
