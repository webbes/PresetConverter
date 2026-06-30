using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PresetConverter;

[TestClass]
public sealed class FilamentPresetSelectorTests
{
    [TestMethod]
    [TestCategory("Core")]
    [Description("Generic template materials and brand presets are kept while unchanged variants are skipped")]
    public void Generic_template_materials_and_brand_presets_are_kept_while_unchanged_variants_are_skipped()
    {
        // Arrange
        var selector = new FilamentPresetSelector();
        var presets = new FilamentPreset[]
        {
            Create("PLA", "Templates.ini"),
            Create("Generic PLA @MK4S", "PrusaResearch.ini"),
            Create("Generic PLA @MK4S HF0.4", "PrusaResearch.ini"),
            Create("Filamentum PLA", "Templates.ini", vendor: "Filamentum"),
            Create("Filamentum PLA @MK4S", "PrusaResearch.ini", vendor: "Filamentum"),
            Create("Filamentum PLA @MK4S HF0.4", "PrusaResearch.ini", vendor: "Filamentum"),
            Create("Filamentum PLA @HF0.6", "PrusaResearch.ini", vendor: "Filamentum", temperature: 220)
        };

        // Act
        var selected = selector.Select(presets).Cast<FilamentPreset>().ToArray();

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                "PLA",
                "Filamentum PLA",
                "Filamentum PLA @HF0.6"
            },
            selected.Select(preset => preset.PresetId).ToArray());
    }

    [TestMethod]
    [TestCategory("Core")]
    [Description("Specific machine variants are kept when their settings differ from the less specific preset")]
    public void Specific_machine_variants_are_kept_when_settings_differ_from_less_specific_preset()
    {
        // Arrange
        var selector = new FilamentPresetSelector();
        var presets = new FilamentPreset[]
        {
            Create("Prusa PLA", "PrusaResearch.ini", vendor: "Prusa"),
            Create("Prusa PLA @MK4S", "PrusaResearch.ini", vendor: "Prusa", temperature: 220)
        };

        // Act
        var selected = selector.Select(presets).Cast<FilamentPreset>().ToArray();

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                "Prusa PLA",
                "Prusa PLA @MK4S"
            },
            selected.Select(preset => preset.PresetId).ToArray());
    }

    [TestMethod]
    [TestCategory("Core")]
    [Description("A broadly compatible preset is preferred over an equivalent printer-specific preset")]
    public void Broadly_compatible_preset_is_preferred_over_equivalent_printer_specific_preset()
    {
        // Arrange
        var selector = new FilamentPresetSelector();
        var presets = new FilamentPreset[]
        {
            Create("Filamentum PLA @Template", "Templates.ini", vendor: "Filamentum"),
            Create(
                "Filamentum PLA",
                "PrusaResearch.ini",
                vendor: "Filamentum",
                compatiblePrinterCondition: "printer_model==\"MK4S\"")
        };

        // Act
        var selected = selector.Select(presets).Cast<FilamentPreset>().ToArray();

        // Assert
        Assert.AreEqual(1, selected.Length);
        Assert.AreEqual("Filamentum PLA", selected[0].PresetId);
        Assert.AreEqual("Templates.ini", selected[0].SourceName);
    }

    [TestMethod]
    [TestCategory("Core")]
    [Description("A broadly compatible variant is not removed when the only equivalent parent is printer-specific")]
    public void Broadly_compatible_variant_is_not_removed_when_only_equivalent_parent_is_printer_specific()
    {
        // Arrange
        var selector = new FilamentPresetSelector();
        var presets = new FilamentPreset[]
        {
            Create(
                "Filamentum PLA",
                "PrusaResearch.ini",
                vendor: "Filamentum",
                compatiblePrinterCondition: "printer_model==\"MK4S\""),
            Create("Filamentum PLA @HF0.6", "PrusaResearch.ini", vendor: "Filamentum")
        };

        // Act
        var selected = selector.Select(presets).Cast<FilamentPreset>().ToArray();

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                "Filamentum PLA",
                "Filamentum PLA @HF0.6"
            },
            selected.Select(preset => preset.PresetId).ToArray());
    }

    private static FilamentPreset Create(
        string presetId,
        string sourceName,
        string materialType = "PLA",
        string? vendor = null,
        int temperature = 210,
        string? compatiblePrinterCondition = null) =>
        new(presetId, sourceName, null)
        {
            PresetId = presetId,
            MaterialType = materialType,
            Vendor = vendor,
            CompatiblePrinterCondition = compatiblePrinterCondition,
            Diameter = 1.75m,
            NozzleTemperature = temperature,
            InitialLayerNozzleTemperature = temperature + 5,
            BedTemperature = 60,
            InitialLayerBedTemperature = 60,
            MaxVolumetricSpeed = 15
        };
}
