namespace PresetConverter;

internal sealed class CommandLineOptions
{
    private bool _onExistingProvided;
    private bool _printerSpecificGCodeProvided;
    private bool _nozzleSizeProvided;
    private bool _forceOutputProvided;
    private bool _prusaConfigurationDirectoryProvided;
    private bool _presetIdPatternProvided;

    public List<string> Inputs { get; } = [];

    public string? OutputDirectory { get; private set; }

    public string? PrusaConfigurationDirectory { get; private set; }

    public string? PresetIdPattern { get; private set; }

    public ExistingFileMode OnExisting { get; private set; }

    public PrinterSpecificGCodeMode PrinterSpecificGCode { get; private set; }

    public decimal? NozzleSize { get; private set; }

    public bool ForceOutputDirectory { get; private set; }

    public bool ShowHelp { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "--input":
                case "-i":
                    RequireValue(args, ++i, arg);
                    while (i < args.Length && !args[i].StartsWith("-", StringComparison.Ordinal))
                    {
                        options.Inputs.Add(args[i]);
                        i++;
                    }

                    i--;
                    break;
                case "--outdir":
                case "-o":
                    options.OutputDirectory = RequireValue(args, ++i, arg);
                    break;
                case "--prusa-config-dir":
                    options.PrusaConfigurationDirectory = RequireValue(args, ++i, arg);
                    options._prusaConfigurationDirectoryProvided = true;
                    break;
                case "--preset-id-pattern":
                    options.PresetIdPattern = RequireValue(args, ++i, arg);
                    options._presetIdPatternProvided = true;
                    break;
                case "--on-existing":
                    options.OnExisting = RequireValue(args, ++i, arg).ToLowerInvariant() switch
                    {
                        "skip" => ExistingFileMode.Skip,
                        "overwrite" => ExistingFileMode.Overwrite,
                        "merge" => ExistingFileMode.Merge,
                        _ => throw new ArgumentException("--on-existing must be skip, overwrite, or merge.")
                    };
                    options._onExistingProvided = true;
                    break;
                case "--overwrite":
                    options.OnExisting = ExistingFileMode.Overwrite;
                    options._onExistingProvided = true;
                    break;
                case "--printer-specific-gcode":
                    options.PrinterSpecificGCode = RequireValue(args, ++i, arg).ToLowerInvariant() switch
                    {
                        "remove-brand-blocks" => PrinterSpecificGCodeMode.RemoveBrandBlocks,
                        "remove-all" => PrinterSpecificGCodeMode.RemoveAll,
                        "keep-all" => PrinterSpecificGCodeMode.KeepAll,
                        _ => throw new ArgumentException("--printer-specific-gcode must be remove-brand-blocks, remove-all, or keep-all.")
                    };
                    options._printerSpecificGCodeProvided = true;
                    break;
                case "--nozzle-size":
                    options.NozzleSize = decimal.Parse(RequireValue(args, ++i, arg), System.Globalization.CultureInfo.InvariantCulture);
                    options._nozzleSizeProvided = true;
                    break;
                case "--force-output":
                    options.ForceOutputDirectory = true;
                    options._forceOutputProvided = true;
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option '{arg}'.");
                    }

                    options.Inputs.Add(arg);
                    break;
            }
        }

        return options;
    }

    public void ApplyTo(ConverterHostOptions options)
    {
        if (Inputs.Count > 0)
        {
            options.Inputs = [.. Inputs];
        }

        if (!string.IsNullOrWhiteSpace(OutputDirectory))
        {
            options.OutputDirectory = OutputDirectory;
        }

        if (_onExistingProvided)
        {
            options.OnExisting = OnExisting;
        }

        if (_forceOutputProvided)
        {
            options.ForceOutputDirectory = ForceOutputDirectory;
        }
    }

    public void ApplyTo(PrusaPresetReaderOptions options)
    {
        if (_prusaConfigurationDirectoryProvided)
        {
            options.ConfigurationDirectory = PrusaConfigurationDirectory!;
        }

        if (_presetIdPatternProvided)
        {
            options.PresetIdPattern = PresetIdPattern!;
        }
    }

    public void ApplyTo(OrcaPresetWriterOptions options)
    {
        if (_printerSpecificGCodeProvided)
        {
            options.PrinterSpecificGCode = PrinterSpecificGCode;
        }

        if (_nozzleSizeProvided)
        {
            options.NozzleSize = NozzleSize;
        }
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
        PresetConverter

        Converts PrusaSlicer/SuperSlicer .ini profiles or config bundles to OrcaSlicer .json user presets.

        Usage:
          PresetConverter [--input <file|directory|pattern> [more inputs]] [options]

        Options:
          --input, -i <file|directory|pattern> Prusa/SuperSlicer .ini file, directory, or wildcard. Defaults to Prusa config folder patterns.
          --prusa-config-dir <directory>       PrusaSlicer configuration directory used when no explicit input is supplied.
          --preset-id-pattern <regex>          Prusa preset id regex. Defaults to generic presets without @.
          --outdir <directory>                 Output directory. Defaults to an Output folder beside the application.
          --on-existing <skip|overwrite|merge> Defaults to skip.
          --printer-specific-gcode <mode>      remove-brand-blocks (default), remove-all, or keep-all.
          --nozzle-size <mm>                   Used when percent-based print profile settings need nozzle width.
          --force-output                       Write JSON files directly into --outdir instead of Orca user/default subfolders. Enabled by default.
          -h, --help                           Show this help.

        Settings can also be provided in appsettings.json under the PresetConverter, Prusa, and Orca sections.
        Command-line values override appsettings.json values where supported.
        """);
    }

    private static string RequireValue(string[] args, int index, string option)
    {
        if (index >= args.Length || args[index].StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index];
    }
}
