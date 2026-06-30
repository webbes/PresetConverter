using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PresetConverter;

internal sealed class PrusaPresetReader(
    IOptions<PrusaPresetReaderOptions> options,
    ILogger<PrusaPresetReader> log) : IPresetReader
{
    private readonly PrusaPresetReaderOptions _options = options.Value;
    private readonly ILogger<PrusaPresetReader> _log = log;

    public IEnumerable<PresetDocument> ReadPresets(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        using var activity = PrusaTelemetry.ActivitySource.StartActivity("Read Prusa presets");
        activity?.SetTag("preset.source", sourceName);
        var started = Stopwatch.GetTimestamp();

        using var reader = new StreamReader(stream, leaveOpen: true);
        var sections = PrusaPresetSectionReader.ReadSections(reader, sourceName).ToList();

        if (sections.Count == 0)
        {
            RecordReadDuration(started);
            yield break;
        }

        if (sections.Count == 1 && sections[0].Name.Length == 0)
        {
            var section = sections[0];
            var kind = PrusaPresetKindDetector.Detect(section.Settings, null);
            if (kind == PresetKind.Filament)
            {
                var name = Path.GetFileNameWithoutExtension(sourceName);
                var preset = CreateFilamentPreset(section with { Kind = kind.Value }, name, name, section.Settings);
                PrusaTelemetry.PresetsRead.Add(1, KeyValuePair.Create<string, object?>("preset.kind", preset.Kind.ToString()));
                yield return preset;
            }

            RecordReadDuration(started);
            yield break;
        }

        var sectionMap = sections.ToDictionary(section => (section.Kind, section.Name));
        foreach (var section in sections)
        {
            if (section.Kind == PresetKind.PhysicalPrinter)
            {
                continue;
            }

            IReadOnlyDictionary<string, string> flattened;
            try
            {
                flattened = PrusaPresetInheritanceResolver.Flatten(section, sectionMap);
            }
            catch (InvalidOperationException ex) when (_options.SkipIncompletePresets)
            {
                _log.LogWarning(ex, "Skipping preset {PresetKind}:{PresetName} from {SourceName}.", section.RawKind, section.Name, sourceName);
                PrusaTelemetry.PresetsSkipped.Add(1, KeyValuePair.Create<string, object?>("skip.reason", "incomplete"));
                continue;
            }

            if (section.Kind != PresetKind.Filament)
            {
                _log.LogDebug("Skipping {PresetKind}:{PresetName} because no canonical model mapping exists yet.", section.RawKind, section.Name);
                PrusaTelemetry.PresetsSkipped.Add(1, KeyValuePair.Create<string, object?>("skip.reason", "unsupported-kind"));
                continue;
            }

            string? templatePresetId = null;
            if (section.Name.StartsWith('*') && !TryResolveGenericTemplatePresetId(section, flattened, out templatePresetId))
            {
                continue;
            }

            var presetId = templatePresetId ?? ResolvePresetId(section);

            if (ShouldSkipByPresetIdPattern(presetId))
            {
                _log.LogInformation(
                    "Skipping preset {PresetKind}:{PresetId} from {SourceName} because it does not match preset id pattern {PresetIdPattern}.",
                    section.RawKind,
                    presetId,
                    sourceName,
                    _options.PresetIdPattern);
                PrusaTelemetry.PresetsSkipped.Add(1, KeyValuePair.Create<string, object?>("skip.reason", "preset-id-pattern"));
                continue;
            }

            var settings = new Dictionary<string, string>(flattened, StringComparer.Ordinal);
            settings.Remove("inherits");
            var preset = CreateFilamentPreset(section, ResolveProfileName(presetId, settings), presetId, settings);
            PrusaTelemetry.PresetsRead.Add(1, KeyValuePair.Create<string, object?>("preset.kind", preset.Kind.ToString()));
            yield return preset;
        }

        RecordReadDuration(started);
    }

    private static void RecordReadDuration(long started) =>
        PrusaTelemetry.ReadDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private bool ShouldSkipByPresetIdPattern(string presetId)
    {
        if (string.IsNullOrWhiteSpace(_options.PresetIdPattern))
        {
            return false;
        }

        return !Regex.IsMatch(presetId, _options.PresetIdPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string ResolvePresetId(PrusaPresetSection section)
    {
        if (section.IsTemplateProfile && section.Kind == PresetKind.Filament && !section.Name.EndsWith(" @Template", StringComparison.Ordinal))
        {
            return section.Name + " @Template";
        }

        return section.Name;
    }

    private static bool TryResolveGenericTemplatePresetId(PrusaPresetSection section, IReadOnlyDictionary<string, string> settings, out string presetId)
    {
        presetId = string.Empty;
        if (!section.IsTemplateProfile)
        {
            return false;
        }

        presetId = GetString(settings, "filament_type") switch
        {
            "PLA" => "PLA",
            "PET" or "PETG" => "PETG",
            "ABS" when section.Name.Equals("*ABS*", StringComparison.Ordinal) => "ABS",
            "FLEX" => "TPU",
            _ => string.Empty
        };

        return presetId.Length > 0;
    }

    private static FilamentPreset CreateFilamentPreset(PrusaPresetSection section, string name, string presetId, IReadOnlyDictionary<string, string> settings) =>
        new(name, section.SourceName, section.SlicerFlavor)
        {
            PresetId = presetId,
            MaterialType = GetString(settings, "filament_type") switch
            {
                "PET" => "PETG",
                "FLEX" => "TPU",
                "NYLON" => "PA",
                var value => value
            },
            Color = GetString(settings, "filament_colour"),
            Vendor = GetString(settings, "filament_vendor"),
            Diameter = GetDecimal(settings, "filament_diameter"),
            Density = GetDecimal(settings, "filament_density"),
            Cost = GetDecimal(settings, "filament_cost"),
            IsSoluble = GetBool(settings, "filament_soluble"),
            FlowRatio = GetDecimal(settings, "extrusion_multiplier"),
            MaxVolumetricSpeed = ResolveMaxVolumetricSpeed(settings),
            NozzleTemperature = GetInt(settings, "temperature"),
            InitialLayerNozzleTemperature = GetInt(settings, "first_layer_temperature"),
            NozzleTemperatureRangeLow = GetInt(settings, "nozzle_temperature_range_low"),
            NozzleTemperatureRangeHigh = GetInt(settings, "nozzle_temperature_range_high"),
            BedTemperature = GetInt(settings, "bed_temperature"),
            InitialLayerBedTemperature = GetInt(settings, "first_layer_bed_temperature"),
            SlowDownLayerTime = GetDecimal(settings, "slowdown_below_layer_time"),
            FanMinSpeed = GetDecimal(settings, "min_fan_speed"),
            FanMaxSpeed = GetDecimal(settings, "max_fan_speed"),
            FanCoolingLayerTime = GetDecimal(settings, "fan_below_layer_time"),
            CloseFanFirstLayers = GetInt(settings, "disable_fan_first_layers"),
            FullFanSpeedLayer = GetInt(settings, "full_fan_speed_layer"),
            KeepFanAlwaysOn = GetBool(settings, "fan_always_on"),
            CoolingMoves = GetInt(settings, "filament_cooling_moves"),
            CoolingInitialSpeed = GetDecimal(settings, "filament_cooling_initial_speed"),
            CoolingFinalSpeed = GetDecimal(settings, "filament_cooling_final_speed"),
            ShrinkageCompensationXY = GetDecimal(settings, "filament_shrinkage_compensation_xy"),
            ShrinkageCompensationZ = GetString(settings, "filament_shrinkage_compensation_z"),
            LoadingSpeed = GetDecimal(settings, "filament_loading_speed"),
            LoadingSpeedStart = GetDecimal(settings, "filament_loading_speed_start"),
            UnloadingSpeed = GetDecimal(settings, "filament_unloading_speed"),
            UnloadingSpeedStart = GetDecimal(settings, "filament_unloading_speed_start"),
            MinimalPurgeOnWipeTower = GetDecimal(settings, "filament_minimal_purge_on_wipe_tower"),
            IsMultiToolRammingEnabled = GetBool(settings, "filament_multitool_ramming"),
            MultiToolRammingFlow = GetDecimal(settings, "filament_multitool_ramming_flow"),
            MultiToolRammingVolume = GetDecimal(settings, "filament_multitool_ramming_volume"),
            RammingParameters = GetString(settings, "filament_ramming_parameters"),
            StampingDistance = GetDecimal(settings, "filament_stamping_distance"),
            StampingLoadingSpeed = GetDecimal(settings, "filament_stamping_loading_speed"),
            ToolChangeDelay = GetDecimal(settings, "filament_toolchange_delay"),
            CompatiblePrinterCondition = GetString(settings, "compatible_printers_condition"),
            CompatiblePrinters = GetString(settings, "compatible_printers"),
            CompatiblePrintCondition = GetString(settings, "compatible_prints_condition"),
            CompatiblePrints = GetString(settings, "compatible_prints"),
            StartGCode = GetSlicerString(settings, "start_filament_gcode"),
            EndGCode = GetSlicerString(settings, "end_filament_gcode"),
            Notes = GetSlicerString(settings, "filament_notes")
        };

    private static decimal? ResolveMaxVolumetricSpeed(IReadOnlyDictionary<string, string> settings)
    {
        var speed = GetDecimal(settings, "filament_max_volumetric_speed");
        if (speed is > 0)
        {
            return speed;
        }

        return GetString(settings, "filament_type") switch
        {
            "PLA" => 15,
            "PET" => 10,
            "ABS" or "ASA" or "NYLON" or "PVA" or "PC" => 12,
            "FLEX" => 3.2m,
            "PSU" or "HIPS" => 8,
            _ => 8
        };
    }

    private static string ResolveProfileName(string internalName, IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue("alias", out var alias) && !string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim();
        }

        var vendorSuffix = internalName.IndexOf('@', StringComparison.Ordinal);
        return vendorSuffix > 0 ? internalName[..vendorSuffix].TrimEnd() : internalName;
    }

    private static string? GetString(IReadOnlyDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && value != "nil" ? value : null;

    private static string? GetSlicerString(IReadOnlyDictionary<string, string> settings, string key)
    {
        var value = GetString(settings, key);
        if (value is null)
        {
            return null;
        }

        var unquoted = value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
        return System.Text.RegularExpressions.Regex.Unescape(unquoted);
    }

    private static decimal? GetDecimal(IReadOnlyDictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value)
            && decimal.TryParse(value.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? GetInt(IReadOnlyDictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => null
        };
    }
}
