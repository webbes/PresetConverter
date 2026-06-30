using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PresetConverter;

[TestClass]
public sealed class OrcaPresetWriterTests
{
    [TestMethod]
    [TestCategory("Orca")]
    [Description("Filament presets are sanitized and written as Orca JSON")]
    public void Filament_presets_are_sanitized_and_written_as_orca_json()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddOrca(options =>
        {
            options.PrinterSpecificGCode = PrinterSpecificGCodeMode.RemoveBrandBlocks;
        });
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IPresetWriter>();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "PresetConverter.Orca.Tests", Guid.NewGuid().ToString("N"));
        var preset = new FilamentPreset(
            "Prusament PLA",
            "bundle.ini:filament:Prusament PLA",
            "PrusaSlicer")
        {
            PresetId = "Prusament PLA @MMU",
            MaterialType = "PLA",
            Color = "#FF8000",
            Diameter = 1.75m,
            Density = 1.24m,
            Cost = 29.99m,
            IsSoluble = true,
            MaxVolumetricSpeed = 3,
            NozzleTemperature = 205,
            InitialLayerNozzleTemperature = 215,
            BedTemperature = 55,
            InitialLayerBedTemperature = 60,
            FanMinSpeed = 35,
            FanMaxSpeed = 100,
            FanCoolingLayerTime = 20,
            CloseFanFirstLayers = 3,
            KeepFanAlwaysOn = true,
            FullFanSpeedLayer = 5,
            CoolingMoves = 4,
            CoolingInitialSpeed = 2.2m,
            CoolingFinalSpeed = 3.3m,
            ShrinkageCompensationXY = 1.25m,
            ShrinkageCompensationZ = "99.5%",
            LoadingSpeed = 28,
            LoadingSpeedStart = 3,
            UnloadingSpeed = 90,
            UnloadingSpeedStart = 100,
            MinimalPurgeOnWipeTower = 15,
            IsMultiToolRammingEnabled = true,
            MultiToolRammingFlow = 10,
            MultiToolRammingVolume = 20,
            RammingParameters = "120 100 6.6 6.8 7.2",
            StampingDistance = 42,
            StampingLoadingSpeed = 33,
            ToolChangeDelay = 1.5m,
            Notes = "",
            CompatiblePrinterCondition = "nozzle_diameter[0]==0.25 and printer_notes=~/.*PRINTER_VENDOR_PRUSA3D.*/ and single_extruder_multi_material",
            StartGCode = "M117 Generic PLA\n{if printer_notes=~/.*PRINTER_VENDOR_PRUSA3D.*/}\nM900 K0.05\n{endif}\n{if printer_notes!~/.*(MK3.5|MINIIS).*/}\n{endif}\nM221 S95\n",
            EndGCode = "; Filament-specific end gcode"
        };

        // Act
        var results = writer.WritePresets([preset], new PresetWriteRequest
        {
            OutputDirectory = outputDirectory,
            ForceOutputDirectory = true,
            ExistingFileMode = ExistingFileMode.Overwrite
        }).ToArray();

        // Assert
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(ConversionItemStatus.Succeeded, results[0].Status);
        using var document = JsonDocument.Parse(File.ReadAllText(results[0].OutputPath!));
        var root = document.RootElement;
        Assert.AreEqual("Prusament PLA @MMU.json", Path.GetFileName(results[0].OutputPath));
        Assert.AreEqual("Prusament PLA @MMU", root.GetProperty("filament_settings_id").GetString());
        Assert.AreEqual("Prusament PLA @MMU", root.GetProperty("name").GetString());
        Assert.IsFalse(root.TryGetProperty("default_filament_colour", out _));
        Assert.AreEqual("#FF8000", root.GetProperty("filament_colour").GetString());
        Assert.AreEqual("1", root.GetProperty("filament_colour_type").GetString());
        Assert.AreEqual("1.24", root.GetProperty("filament_density").GetString());
        Assert.AreEqual("29.99", root.GetProperty("filament_cost").GetString());
        Assert.AreEqual("1", root.GetProperty("filament_soluble").GetString());
        Assert.AreEqual("190", root.GetProperty("nozzle_temperature_range_low").GetString());
        Assert.AreEqual("230", root.GetProperty("nozzle_temperature_range_high").GetString());
        Assert.AreEqual("55", root.GetProperty("graphic_effect_plate_temp").GetString());
        Assert.AreEqual("60", root.GetProperty("graphic_effect_plate_temp_initial_layer").GetString());
        Assert.AreEqual("35", root.GetProperty("fan_min_speed").GetString());
        Assert.AreEqual("100", root.GetProperty("fan_max_speed").GetString());
        Assert.AreEqual("20", root.GetProperty("fan_cooling_layer_time").GetString());
        Assert.AreEqual("3", root.GetProperty("close_fan_the_first_x_layers").GetString());
        Assert.AreEqual("1", root.GetProperty("reduce_fan_stop_start_freq").GetString());
        Assert.AreEqual("5", root.GetProperty("full_fan_speed_layer").GetString());
        Assert.AreEqual("4", root.GetProperty("filament_cooling_moves").GetString());
        Assert.AreEqual("2.2", root.GetProperty("filament_cooling_initial_speed").GetString());
        Assert.AreEqual("3.3", root.GetProperty("filament_cooling_final_speed").GetString());
        Assert.AreEqual("98.75%", root.GetProperty("filament_shrink").GetString());
        Assert.AreEqual("99.5%", root.GetProperty("filament_shrinkage_compensation_z").GetString());
        Assert.AreEqual("28", root.GetProperty("filament_loading_speed").GetString());
        Assert.AreEqual("3", root.GetProperty("filament_loading_speed_start").GetString());
        Assert.AreEqual("90", root.GetProperty("filament_unloading_speed").GetString());
        Assert.AreEqual("100", root.GetProperty("filament_unloading_speed_start").GetString());
        Assert.AreEqual("15", root.GetProperty("filament_minimal_purge_on_wipe_tower").GetString());
        Assert.AreEqual("1", root.GetProperty("filament_multitool_ramming").GetString());
        Assert.AreEqual("10", root.GetProperty("filament_multitool_ramming_flow").GetString());
        Assert.AreEqual("20", root.GetProperty("filament_multitool_ramming_volume").GetString());
        Assert.AreEqual("120 100 6.6 6.8 7.2", root.GetProperty("filament_ramming_parameters").GetString());
        Assert.AreEqual("42", root.GetProperty("filament_stamping_distance").GetString());
        Assert.AreEqual("33", root.GetProperty("filament_stamping_loading_speed").GetString());
        Assert.AreEqual("1.5", root.GetProperty("filament_toolchange_delay").GetString());
        Assert.AreEqual("nozzle_diameter[0]==0.25 and printer_notes=~/.*PRINTER_VENDOR_PRUSA3D.*/ and single_extruder_multi_material", root.GetProperty("compatible_printers_condition").GetString());
        Assert.IsFalse(root.TryGetProperty("filament_notes", out _));
        Assert.IsFalse(root.TryGetProperty("filament_end_gcode", out _));
        var startGCode = root.GetProperty("filament_start_gcode")[0].GetString();
        Assert.AreEqual("M117 Generic PLA\nM221 S95", startGCode);
    }

    [TestMethod]
    [TestCategory("Orca")]
    [Description("Explicit nozzle temperature ranges override material defaults")]
    public void Explicit_nozzle_temperature_ranges_override_material_defaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddOrca();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IPresetWriter>();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "PresetConverter.Orca.Tests", Guid.NewGuid().ToString("N"));
        var preset = new FilamentPreset("PLA With Custom Range", "bundle.ini", "PrusaSlicer")
        {
            MaterialType = "PLA",
            NozzleTemperature = 205,
            InitialLayerNozzleTemperature = 215,
            NozzleTemperatureRangeLow = 185,
            NozzleTemperatureRangeHigh = 245
        };

        // Act
        var result = writer.WritePresets([preset], new PresetWriteRequest
        {
            OutputDirectory = outputDirectory,
            ForceOutputDirectory = true,
            ExistingFileMode = ExistingFileMode.Overwrite
        }).Single();

        // Assert
        Assert.AreEqual(ConversionItemStatus.Succeeded, result.Status);
        using var document = JsonDocument.Parse(File.ReadAllText(result.OutputPath!));
        var root = document.RootElement;
        Assert.AreEqual("185", root.GetProperty("nozzle_temperature_range_low").GetString());
        Assert.AreEqual("245", root.GetProperty("nozzle_temperature_range_high").GetString());
    }

    [TestMethod]
    [TestCategory("Orca")]
    [Description("Preset ids prevent filename collisions for shared display names")]
    public void Preset_ids_prevent_filename_collisions_for_shared_display_names()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddOrca();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IPresetWriter>();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "PresetConverter.Orca.Tests", Guid.NewGuid().ToString("N"));

        var firstPreset = new FilamentPreset("Prusament PLA", "bundle.ini", "PrusaSlicer")
        {
            PresetId = "Prusament PLA @0.25 nozzle",
            MaterialType = "PLA"
        };
        var secondPreset = firstPreset with
        {
            PresetId = "Prusament PLA @0.4 nozzle"
        };

        // Act
        var results = writer.WritePresets([firstPreset, secondPreset], new PresetWriteRequest
        {
            OutputDirectory = outputDirectory,
            ForceOutputDirectory = true,
            ExistingFileMode = ExistingFileMode.Skip
        }).ToArray();

        // Assert
        Assert.AreEqual(2, results.Length);
        Assert.IsTrue(results.All(result => result.Status == ConversionItemStatus.Succeeded));
        Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Prusament PLA @0.25 nozzle.json")));
        Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Prusament PLA @0.4 nozzle.json")));
    }
}
