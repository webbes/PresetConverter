using System.Text.RegularExpressions;

namespace PresetConverter;

public sealed partial class FilamentPresetSelector
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public IEnumerable<PresetDocument> Select(IEnumerable<PresetDocument> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var otherPresets = new List<PresetDocument>();
        var filamentPresets = new List<FilamentPreset>();
        foreach (var preset in presets)
        {
            if (preset is FilamentPreset filamentPreset)
            {
                filamentPresets.Add(filamentPreset);
            }
            else
            {
                otherPresets.Add(preset);
            }
        }

        foreach (var preset in otherPresets)
        {
            yield return preset;
        }

        var selected = SelectFilaments(filamentPresets);
        foreach (var preset in selected)
        {
            yield return preset;
        }
    }

    private static IEnumerable<FilamentPreset> SelectFilaments(IReadOnlyCollection<FilamentPreset> presets)
    {
        var candidates = presets
            .Select(preset => new FilamentPresetCandidate(preset, FilamentPresetIdentity.Parse(preset)))
            .GroupBy(candidate => candidate.Identity.OutputId, StringComparer.Ordinal)
            .Select(ChoosePreferredCandidate)
            .OrderBy(candidate => candidate.Identity.SortRank)
            .ThenBy(candidate => candidate.Identity.OutputId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new Dictionary<string, FilamentPresetCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (ShouldKeep(candidate, selected))
            {
                selected[candidate.Identity.OutputId] = candidate;
                yield return candidate.Preset with { PresetId = candidate.Identity.OutputId, Name = candidate.Identity.OutputId };
            }
        }
    }

    private static FilamentPresetCandidate ChoosePreferredCandidate(IEnumerable<FilamentPresetCandidate> candidates) =>
        candidates
            .OrderBy(candidate => candidate.Identity.CompatibilitySpecificity)
            .ThenBy(candidate => candidate.Identity.IsTemplateSource ? 1 : 0)
            .ThenBy(candidate => candidate.Preset.SourceName, StringComparer.OrdinalIgnoreCase)
            .First();

    private static bool ShouldKeep(
        FilamentPresetCandidate candidate,
        IReadOnlyDictionary<string, FilamentPresetCandidate> selected)
    {
        if (!candidate.Identity.IsVariant)
        {
            return true;
        }

        foreach (var parentId in candidate.Identity.ParentIds)
        {
            if (selected.TryGetValue(parentId, out var parent)
                && parent.Identity.CompatibilitySpecificity <= candidate.Identity.CompatibilitySpecificity
                && FilamentPresetContentComparer.Equals(candidate.Preset, parent.Preset))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record FilamentPresetCandidate(
        FilamentPreset Preset,
        FilamentPresetIdentity Identity);

    private sealed partial record FilamentPresetIdentity(
        string OutputId,
        int SortRank,
        bool IsVariant,
        bool IsTemplateSource,
        bool IsTemplatePreset,
        int CompatibilitySpecificity,
        IReadOnlyList<string> ParentIds)
    {
        public static FilamentPresetIdentity Parse(FilamentPreset preset)
        {
            var presetId = preset.PresetId.Trim();
            var isTemplatePreset = presetId.EndsWith(" @Template", StringComparison.OrdinalIgnoreCase);
            var withoutTemplateSuffix = isTemplatePreset ? presetId[..^" @Template".Length].TrimEnd() : presetId;
            var isTemplateSource = preset.SourceName.EndsWith("Templates.ini", StringComparison.OrdinalIgnoreCase);

            var materialType = NormalizeMaterialType(preset.MaterialType);
            if (isTemplateSource && IsGenericMaterialId(withoutTemplateSuffix, materialType))
            {
                return new FilamentPresetIdentity(materialType, 0, false, true, false, GetCompatibilitySpecificity(preset), []);
            }

            var (baseId, variant) = SplitVariant(withoutTemplateSuffix);
            var isGenericBase = IsGenericMaterialId(baseId, materialType);
            var normalizedBaseId = isGenericBase ? materialType : baseId;
            var hasNozzle = HasNozzle(variant);
            var hasMachine = variant.Length > 0 && !HasNozzleOnly(variant);
            var normalizedVariant = NormalizeVariant(variant);
            var outputId = normalizedVariant.Length == 0 ? normalizedBaseId : $"{normalizedBaseId} @{normalizedVariant}";
            var sortRank = GetSortRank(isGenericBase, hasMachine, hasNozzle);
            var parentIds = GetParentIds(normalizedBaseId, normalizedVariant, hasMachine, hasNozzle);

            return new FilamentPresetIdentity(outputId, sortRank, normalizedVariant.Length > 0, isTemplateSource, isTemplatePreset, GetCompatibilitySpecificity(preset), parentIds);
        }

        private static int GetCompatibilitySpecificity(FilamentPreset preset)
        {
            var condition = preset.CompatiblePrinterCondition;
            if (string.IsNullOrWhiteSpace(condition))
            {
                return 0;
            }

            var withoutNegatedGroups = NegatedPrinterExpressionRegex().Replace(condition, string.Empty);
            if (SpecificPrinterModelRegex().IsMatch(withoutNegatedGroups)
                || PositivePrinterMarkerRegex().IsMatch(withoutNegatedGroups))
            {
                return 2;
            }

            return 1;
        }

        private static string NormalizeMaterialType(string? materialType) =>
            materialType?.Trim().ToUpperInvariant() switch
            {
                "PET" or "PETG" => "PETG",
                "FLEX" or "TPU" => "TPU",
                "NYLON" => "PA",
                { Length: > 0 } value => value,
                _ => "FILAMENT"
            };

        private static bool IsGenericMaterialId(string presetId, string materialType)
        {
            var normalized = presetId.Trim().Trim('*');
            return Comparer.Equals(normalized, materialType)
                || Comparer.Equals(normalized, "Generic " + materialType)
                || (Comparer.Equals(materialType, "PETG") && Comparer.Equals(normalized, "PET"))
                || (Comparer.Equals(materialType, "TPU") && Comparer.Equals(normalized, "FLEX"));
        }

        private static (string BaseId, string Variant) SplitVariant(string presetId)
        {
            var index = presetId.IndexOf('@', StringComparison.Ordinal);
            return index < 0
                ? (presetId.Trim(), string.Empty)
                : (presetId[..index].Trim(), presetId[(index + 1)..].Trim());
        }

        private static bool HasNozzle(string variant) =>
            NozzleRegex().IsMatch(variant);

        private static bool HasNozzleOnly(string variant)
        {
            if (variant.Length == 0)
            {
                return false;
            }

            var withoutNozzle = NozzleRegex().Replace(variant, string.Empty);
            withoutNozzle = withoutNozzle.Replace("nozzle", string.Empty, StringComparison.OrdinalIgnoreCase);
            return string.IsNullOrWhiteSpace(withoutNozzle);
        }

        private static string NormalizeVariant(string variant) =>
            Regex.Replace(variant.Trim(), @"\s+", " ");

        private static int GetSortRank(bool isGenericBase, bool hasMachine, bool hasNozzle) => (isGenericBase, hasMachine, hasNozzle) switch
        {
            (true, false, false) => 0,
            (true, false, true) => 1,
            (false, false, false) => 2,
            (false, false, true) => 3,
            (true, true, false) => 4,
            (true, true, true) => 5,
            (false, true, false) => 6,
            (false, true, true) => 7
        };

        private static IReadOnlyList<string> GetParentIds(string baseId, string variant, bool hasMachine, bool hasNozzle)
        {
            if (variant.Length == 0)
            {
                return [];
            }

            if (hasMachine && hasNozzle)
            {
                var nozzle = NozzleRegex().Match(variant).Value;
                var machine = NormalizeVariant(NozzleRegex().Replace(variant, string.Empty).Replace("nozzle", string.Empty, StringComparison.OrdinalIgnoreCase));
                return
                [
                    $"{baseId} @{nozzle}",
                    machine.Length == 0 ? baseId : $"{baseId} @{machine}",
                    baseId
                ];
            }

            return [baseId];
        }

        [GeneratedRegex(@"(?:HF)?0\.\d+", RegexOptions.IgnoreCase)]
        private static partial Regex NozzleRegex();

        [GeneratedRegex(@"!\s*\([^)]*PRINTER_(?:MODEL|VENDOR)_[^)]*\)", RegexOptions.IgnoreCase)]
        private static partial Regex NegatedPrinterExpressionRegex();

        [GeneratedRegex(@"\bprinter_model\s*(?:==|=~)", RegexOptions.IgnoreCase)]
        private static partial Regex SpecificPrinterModelRegex();

        [GeneratedRegex(@"\bprinter_notes\s*=~/[^&|;]*PRINTER_(?:MODEL|VENDOR)_", RegexOptions.IgnoreCase)]
        private static partial Regex PositivePrinterMarkerRegex();
    }

    private static class FilamentPresetContentComparer
    {
        public static bool Equals(FilamentPreset left, FilamentPreset right) =>
            Comparer.Equals(left.MaterialType, right.MaterialType)
            && Comparer.Equals(left.Color, right.Color)
            && Comparer.Equals(left.Vendor, right.Vendor)
            && left.Diameter == right.Diameter
            && left.Density == right.Density
            && left.Cost == right.Cost
            && left.IsSoluble == right.IsSoluble
            && left.FlowRatio == right.FlowRatio
            && left.MaxVolumetricSpeed == right.MaxVolumetricSpeed
            && left.NozzleTemperature == right.NozzleTemperature
            && left.InitialLayerNozzleTemperature == right.InitialLayerNozzleTemperature
            && left.NozzleTemperatureRangeLow == right.NozzleTemperatureRangeLow
            && left.NozzleTemperatureRangeHigh == right.NozzleTemperatureRangeHigh
            && left.BedTemperature == right.BedTemperature
            && left.InitialLayerBedTemperature == right.InitialLayerBedTemperature
            && left.FanMinSpeed == right.FanMinSpeed
            && left.FanMaxSpeed == right.FanMaxSpeed
            && left.FanCoolingLayerTime == right.FanCoolingLayerTime
            && left.CloseFanFirstLayers == right.CloseFanFirstLayers
            && left.FullFanSpeedLayer == right.FullFanSpeedLayer
            && left.SlowDownLayerTime == right.SlowDownLayerTime
            && left.KeepFanAlwaysOn == right.KeepFanAlwaysOn
            && left.CoolingMoves == right.CoolingMoves
            && left.CoolingInitialSpeed == right.CoolingInitialSpeed
            && left.CoolingFinalSpeed == right.CoolingFinalSpeed
            && left.ShrinkageCompensationXY == right.ShrinkageCompensationXY
            && Comparer.Equals(left.ShrinkageCompensationZ, right.ShrinkageCompensationZ)
            && left.LoadingSpeed == right.LoadingSpeed
            && left.LoadingSpeedStart == right.LoadingSpeedStart
            && left.UnloadingSpeed == right.UnloadingSpeed
            && left.UnloadingSpeedStart == right.UnloadingSpeedStart
            && left.MinimalPurgeOnWipeTower == right.MinimalPurgeOnWipeTower
            && left.IsMultiToolRammingEnabled == right.IsMultiToolRammingEnabled
            && left.MultiToolRammingFlow == right.MultiToolRammingFlow
            && left.MultiToolRammingVolume == right.MultiToolRammingVolume
            && Comparer.Equals(left.RammingParameters, right.RammingParameters)
            && left.StampingDistance == right.StampingDistance
            && left.StampingLoadingSpeed == right.StampingLoadingSpeed
            && left.ToolChangeDelay == right.ToolChangeDelay
            && Comparer.Equals(left.StartGCode, right.StartGCode)
            && Comparer.Equals(left.EndGCode, right.EndGCode)
            && Comparer.Equals(left.Notes, right.Notes);
    }
}
