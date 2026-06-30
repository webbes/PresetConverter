using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PresetConverter;

internal sealed class OrcaPresetWriter(
    IOptions<OrcaPresetWriterOptions> options,
    ILogger<OrcaPresetWriter> log) : IPresetWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly OrcaPresetWriterOptions _options = options.Value;
    private readonly ILogger<OrcaPresetWriter> _log = log;

    public IEnumerable<ConversionItemResult> WritePresets(IEnumerable<PresetDocument> presets, PresetWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);

        foreach (var preset in presets)
        {
            ConversionItemResult result;
            try
            {
                result = WritePreset(preset, request);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to write Orca preset {PresetName} from {SourceName}.", preset.Name, preset.SourceName);
                OrcaTelemetry.PresetsFailed.Add(1, KeyValuePair.Create<string, object?>("preset.kind", preset.Kind.ToString()));
                result = ConversionItemResult.Failed(preset.SourceName, ex);
            }

            yield return result;
        }
    }

    private ConversionItemResult WritePreset(PresetDocument preset, PresetWriteRequest request)
    {
        using var activity = OrcaTelemetry.ActivitySource.StartActivity("Write Orca preset");
        activity?.SetTag("preset.name", preset.Name);
        activity?.SetTag("preset.kind", preset.Kind.ToString());
        activity?.SetTag("preset.source", preset.SourceName);
        var started = Stopwatch.GetTimestamp();

        if (preset is not FilamentPreset filamentPreset)
        {
            OrcaTelemetry.PresetsSkipped.Add(1, KeyValuePair.Create<string, object?>("skip.reason", "unsupported-kind"));
            OrcaTelemetry.WriteDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return ConversionItemResult.Skipped(
                preset.SourceName,
                new ConversionWarning("UnsupportedPresetKind", $"Orca writing is currently implemented for filament presets. {preset.Kind} was skipped.", preset.SourceName));
        }

        var outputDirectory = request.ForceOutputDirectory
            ? Path.GetFullPath(request.OutputDirectory)
            : Path.Combine(Path.GetFullPath(request.OutputDirectory), "user", "default", "filament");

        Directory.CreateDirectory(outputDirectory);
        var outputFile = Path.Combine(outputDirectory, FileName.Safe(filamentPreset.PresetId) + ".json");

        if (File.Exists(outputFile) && request.ExistingFileMode == ExistingFileMode.Skip)
        {
            OrcaTelemetry.PresetsSkipped.Add(1, KeyValuePair.Create<string, object?>("skip.reason", "target-exists"));
            OrcaTelemetry.WriteDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return ConversionItemResult.Skipped(
                filamentPreset.SourceName,
                new ConversionWarning("TargetExists", $"Target file already exists: {outputFile}", filamentPreset.SourceName));
        }

        var json = CreateFilamentJson(filamentPreset);
        if (File.Exists(outputFile) && request.ExistingFileMode == ExistingFileMode.Merge)
        {
            MergeExisting(json, outputFile);
        }

        var ordered = json.OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        File.WriteAllText(outputFile, JsonSerializer.Serialize(ordered, JsonOptions));
        activity?.SetTag("preset.output", outputFile);
        OrcaTelemetry.PresetsWritten.Add(1, KeyValuePair.Create<string, object?>("preset.kind", filamentPreset.Kind.ToString()));
        OrcaTelemetry.WriteDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return ConversionItemResult.Succeeded(filamentPreset.SourceName, outputFile);
    }

    private Dictionary<string, object?> CreateFilamentJson(FilamentPreset preset)
    {
        var json = new Dictionary<string, object?>(StringComparer.Ordinal);
        AddIdentity(json, preset);
        AddMaterial(json, preset);
        AddExtrusion(json, preset);
        AddTemperatures(json, preset);
        AddCooling(json, preset);
        AddShrinkage(json, preset);
        AddToolChange(json, preset);
        AddCompatibility(json, preset);
        AddGCode(json, preset);
        return json;
    }

    private void AddIdentity(Dictionary<string, object?> json, FilamentPreset preset)
    {
        json["filament_settings_id"] = preset.PresetId;
        json["name"] = preset.PresetId;
        json["from"] = "User";
        json["is_custom_defined"] = "1";
        json["version"] = _options.OrcaSlicerVersion;
    }

    private static void AddMaterial(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "filament_type", preset.MaterialType);
        AddFilamentColor(json, preset.Color);
        AddIfPresent(json, "filament_vendor", preset.Vendor);
        AddIfPresent(json, "filament_diameter", preset.Diameter);
        AddIfPresent(json, "filament_density", preset.Density);
        AddIfPresent(json, "filament_cost", preset.Cost);
        AddIfPresent(json, "filament_soluble", preset.IsSoluble);
    }

    private static void AddFilamentColor(Dictionary<string, object?> json, string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return;
        }

        json["filament_colour"] = color;
        json["filament_colour_type"] = "1";
    }

    private static void AddExtrusion(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "filament_flow_ratio", preset.FlowRatio);
        AddIfPresent(json, "filament_max_volumetric_speed", preset.MaxVolumetricSpeed);
    }

    private static void AddTemperatures(Dictionary<string, object?> json, FilamentPreset preset)
    {
        var defaultRange = ResolveNozzleTemperatureRange(preset.MaterialType);
        json["nozzle_temperature_range_low"] = FormatInt(preset.NozzleTemperatureRangeLow ?? defaultRange.Low);
        json["nozzle_temperature_range_high"] = FormatInt(preset.NozzleTemperatureRangeHigh ?? defaultRange.High);

        AddIfPresent(json, "nozzle_temperature", preset.NozzleTemperature);
        AddIfPresent(json, "nozzle_temperature_initial_layer", preset.InitialLayerNozzleTemperature);
        AddPlateTemperatures(json, preset);
    }

    private static void AddCooling(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "fan_min_speed", preset.FanMinSpeed);
        AddIfPresent(json, "fan_max_speed", preset.FanMaxSpeed);
        AddIfPresent(json, "fan_cooling_layer_time", preset.FanCoolingLayerTime);
        AddIfPresent(json, "close_fan_the_first_x_layers", preset.CloseFanFirstLayers);
        AddIfPresent(json, "full_fan_speed_layer", preset.FullFanSpeedLayer);
        AddIfPresent(json, "slow_down_layer_time", preset.SlowDownLayerTime);
        AddIfPresent(json, "reduce_fan_stop_start_freq", preset.KeepFanAlwaysOn);
        AddIfPresent(json, "filament_cooling_moves", preset.CoolingMoves);
        AddIfPresent(json, "filament_cooling_initial_speed", preset.CoolingInitialSpeed);
        AddIfPresent(json, "filament_cooling_final_speed", preset.CoolingFinalSpeed);

        if (preset.SlowDownLayerTime is not null)
        {
            json["slow_down_for_layer_cooling"] = preset.SlowDownLayerTime > 0 ? "1" : "0";
        }
    }

    private static void AddShrinkage(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "filament_shrink", ConvertShrinkageCompensationXY(preset.ShrinkageCompensationXY));
        AddIfPresent(json, "filament_shrinkage_compensation_z", preset.ShrinkageCompensationZ);
    }

    private static void AddToolChange(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "filament_loading_speed", preset.LoadingSpeed);
        AddIfPresent(json, "filament_loading_speed_start", preset.LoadingSpeedStart);
        AddIfPresent(json, "filament_unloading_speed", preset.UnloadingSpeed);
        AddIfPresent(json, "filament_unloading_speed_start", preset.UnloadingSpeedStart);
        AddIfPresent(json, "filament_minimal_purge_on_wipe_tower", preset.MinimalPurgeOnWipeTower);
        AddIfPresent(json, "filament_multitool_ramming", preset.IsMultiToolRammingEnabled);
        AddIfPresent(json, "filament_multitool_ramming_flow", preset.MultiToolRammingFlow);
        AddIfPresent(json, "filament_multitool_ramming_volume", preset.MultiToolRammingVolume);
        AddIfPresent(json, "filament_ramming_parameters", preset.RammingParameters);
        AddIfPresent(json, "filament_stamping_distance", preset.StampingDistance);
        AddIfPresent(json, "filament_stamping_loading_speed", preset.StampingLoadingSpeed);
        AddIfPresent(json, "filament_toolchange_delay", preset.ToolChangeDelay);
    }

    private void AddCompatibility(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "compatible_printers_condition", preset.CompatiblePrinterCondition);
        AddIfPresent(json, "compatible_printers", preset.CompatiblePrinters);
        AddIfPresent(json, "compatible_prints_condition", preset.CompatiblePrintCondition);
        AddIfPresent(json, "compatible_prints", preset.CompatiblePrints);
    }

    private void AddGCode(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddArrayText(json, "filament_start_gcode", ConvertPrinterSpecificGCode(preset.StartGCode));
        AddArrayText(json, "filament_end_gcode", ConvertPrinterSpecificGCode(preset.EndGCode));
        AddArrayText(json, "filament_notes", preset.Notes);
    }

    private static void AddPlateTemperatures(Dictionary<string, object?> json, FilamentPreset preset)
    {
        AddIfPresent(json, "hot_plate_temp", preset.BedTemperature);
        AddIfPresent(json, "cool_plate_temp", preset.BedTemperature);
        AddIfPresent(json, "eng_plate_temp", preset.BedTemperature);
        AddIfPresent(json, "textured_plate_temp", preset.BedTemperature);
        AddIfPresent(json, "graphic_effect_plate_temp", preset.BedTemperature);
        AddIfPresent(json, "hot_plate_temp_initial_layer", preset.InitialLayerBedTemperature);
        AddIfPresent(json, "cool_plate_temp_initial_layer", preset.InitialLayerBedTemperature);
        AddIfPresent(json, "eng_plate_temp_initial_layer", preset.InitialLayerBedTemperature);
        AddIfPresent(json, "textured_plate_temp_initial_layer", preset.InitialLayerBedTemperature);
        AddIfPresent(json, "graphic_effect_plate_temp_initial_layer", preset.InitialLayerBedTemperature);
    }

    private string? ConvertPrinterSpecificGCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return _options.PrinterSpecificGCode switch
        {
            PrinterSpecificGCodeMode.KeepAll => value,
            PrinterSpecificGCodeMode.RemoveAll => null,
            PrinterSpecificGCodeMode.RemoveBrandBlocks => RemovePrinterBrandGCodeBlocks(value),
            _ => value
        };
    }

    private static string? RemovePrinterBrandGCodeBlocks(string value)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(value, @"(?ms)^[ \t]*\{if[^\r\n{}]*(?:PRINTER_VENDOR_|PRINTER_MODEL_)[^\r\n{}]*\}\s*(?:\r?\n).*?^[ \t]*\{endif\}[ \t]*(?:\r?\n)?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"(?m)^[^\r\n]*(?:PRINTER_VENDOR_|PRINTER_MODEL_)[^\r\n]*(?:\r?\n)?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"(?ms)^[ \t]*\{if[^\r\n{}]*\}\s*(?:\r?\n)?[ \t]*\{endif\}[ \t]*(?:\r?\n)?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"(?:\r?\n){3,}", Environment.NewLine + Environment.NewLine);
        cleaned = cleaned.Trim();

        return cleaned.Length == 0 || IsCommentOnlyGCode(cleaned) ? null : cleaned;
    }

    private static bool IsCommentOnlyGCode(string value)
    {
        return value
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .All(line => line.StartsWith(';'));
    }

    private static void AddIfPresent(Dictionary<string, object?> json, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            json[key] = value;
        }
    }

    private static void AddIfPresent(Dictionary<string, object?> json, string key, decimal? value)
    {
        if (value is not null)
        {
            json[key] = FormatDecimal(value.Value);
        }
    }

    private static void AddIfPresent(Dictionary<string, object?> json, string key, int? value)
    {
        if (value is not null)
        {
            json[key] = FormatInt(value.Value);
        }
    }

    private static void AddIfPresent(Dictionary<string, object?> json, string key, bool? value)
    {
        if (value is not null)
        {
            json[key] = value.Value ? "1" : "0";
        }
    }

    private static void AddArrayText(Dictionary<string, object?> json, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            json[key] = new[] { value };
        }
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);

    private static string FormatInt(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string? ConvertShrinkageCompensationXY(decimal? value) =>
        value is null ? null : $"{FormatDecimal(100 - value.Value)}%";

    private static (int Low, int High) ResolveNozzleTemperatureRange(string? materialType)
    {
        return materialType?.Trim().ToUpperInvariant() switch
        {
            "ABS" or "ASA" => (240, 270),
            "PA" or "NYLON" => (260, 300),
            "PC" => (260, 290),
            "PET" or "PETG" => (220, 260),
            "PLA" => (190, 230),
            "PVA" => (190, 240),
            "TPU" or "FLEX" => (200, 250),
            _ => (190, 240)
        };
    }

    private static void MergeExisting(Dictionary<string, object?> json, string outputFile)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
        foreach (var property in document.RootElement.EnumerateObject())
        {
            json[property.Name] = JsonElementToObject(property.Value);
        }
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
