namespace PresetConverter;

public sealed record FilamentPreset(
    string Name,
    string SourceName,
    string? SlicerFlavor) : PresetDocument(PresetKind.Filament, Name, SourceName, SlicerFlavor)
{
    public string? MaterialType { get; init; }

    public string? Color { get; init; }

    public string? Vendor { get; init; }

    public decimal? Diameter { get; init; }

    public decimal? Density { get; init; }

    public decimal? Cost { get; init; }

    public bool? IsSoluble { get; init; }

    public decimal? FlowRatio { get; init; }

    public decimal? MaxVolumetricSpeed { get; init; }

    public int? NozzleTemperature { get; init; }

    public int? InitialLayerNozzleTemperature { get; init; }

    public int? NozzleTemperatureRangeLow { get; init; }

    public int? NozzleTemperatureRangeHigh { get; init; }

    public int? BedTemperature { get; init; }

    public int? InitialLayerBedTemperature { get; init; }

    public decimal? FanMinSpeed { get; init; }

    public decimal? FanMaxSpeed { get; init; }

    public decimal? FanCoolingLayerTime { get; init; }

    public int? CloseFanFirstLayers { get; init; }

    public int? FullFanSpeedLayer { get; init; }

    public decimal? SlowDownLayerTime { get; init; }

    public bool? KeepFanAlwaysOn { get; init; }

    public int? CoolingMoves { get; init; }

    public decimal? CoolingInitialSpeed { get; init; }

    public decimal? CoolingFinalSpeed { get; init; }

    public decimal? ShrinkageCompensationXY { get; init; }

    public string? ShrinkageCompensationZ { get; init; }

    public decimal? LoadingSpeed { get; init; }

    public decimal? LoadingSpeedStart { get; init; }

    public decimal? UnloadingSpeed { get; init; }

    public decimal? UnloadingSpeedStart { get; init; }

    public decimal? MinimalPurgeOnWipeTower { get; init; }

    public bool? IsMultiToolRammingEnabled { get; init; }

    public decimal? MultiToolRammingFlow { get; init; }

    public decimal? MultiToolRammingVolume { get; init; }

    public string? RammingParameters { get; init; }

    public decimal? StampingDistance { get; init; }

    public decimal? StampingLoadingSpeed { get; init; }

    public decimal? ToolChangeDelay { get; init; }

    public string? CompatiblePrinterCondition { get; init; }

    public string? CompatiblePrinters { get; init; }

    public string? CompatiblePrintCondition { get; init; }

    public string? CompatiblePrints { get; init; }

    public string? StartGCode { get; init; }

    public string? EndGCode { get; init; }

    public string? Notes { get; init; }
}
