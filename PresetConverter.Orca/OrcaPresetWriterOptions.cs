namespace PresetConverter;

public sealed class OrcaPresetWriterOptions
{
    public string OrcaSlicerVersion { get; set; } = "1.6.0.0";

    public PrinterSpecificGCodeMode PrinterSpecificGCode { get; set; } = PrinterSpecificGCodeMode.RemoveBrandBlocks;

    public decimal? NozzleSize { get; set; }
}
