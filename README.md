# PresetConverter

Converts PrusaSlicer and SuperSlicer `.ini` presets into OrcaSlicer `.json` user presets.

PresetConverter is an early-stage converter focused on filament preset conversion. It is useful for inspecting and migrating presets, but generated output should still be reviewed in OrcaSlicer before printing.

This repository is published as-is and is not expected to be actively maintained. If you want to extend it, fix bugs, or take it in a different direction, feel free to fork the project and continue from there.

The solution is split by domain so the conversion logic can be hosted by a console app today and by another application, such as a web upload flow, later.

## Requirements

- .NET 10 SDK
- Windows, macOS, or Linux for explicit `--input` conversion
- PrusaSlicer configuration folders use Windows-style examples in this README, but the converter can also read explicit files or directories on other platforms

## Solution Structure

```text
PresetConverter              Console host and composition root
PresetConverter.Core         Canonical model, service contracts, results, feature collection
PresetConverter.Prusa        Prusa/SuperSlicer readers that map input to the canonical model
PresetConverter.Orca         OrcaSlicer writers that map the canonical model to JSON
PresetConverter.Prusa.Tests  MSTest tests for the public Prusa reader API
PresetConverter.Orca.Tests   MSTest tests for the public Orca writer API
```

## Current Scope

Supported today:

- PrusaSlicer and SuperSlicer `.ini` filament presets
- PrusaSlicer vendor config bundles that contain filament presets
- OrcaSlicer `.json` user preset output
- Basic handling for inherited filament preset values
- Optional filtering of printer or nozzle specific preset variants
- Optional removal of Prusa-specific conditional G-code blocks

Not a goal yet:

- Full print preset conversion
- Full printer preset conversion
- Automatic installation into an OrcaSlicer profile folder
- Guaranteeing that every slicer-specific setting has an exact equivalent

## Build And Test

```powershell
dotnet build PresetConverter.slnx
dotnet test PresetConverter.slnx --no-build
```

## Downloads

Prebuilt downloads are published through GitHub Releases when a version tag is pushed. Each release contains self-contained single-file packages for:

- `win-x64`
- `linux-x64`
- `osx-x64`

Download the package for your operating system from the repository's Releases page, extract it, and run the `PresetConverter` executable. The app is self-contained, so the .NET runtime does not need to be installed on the target machine.

To publish a new release from this repository:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## Run

From the solution root:

```powershell
dotnet PresetConverter\bin\Debug\net10.0\PresetConverter.dll
```

With no `--input`, the converter reads from the PrusaSlicer configuration folder:

```text
%APPDATA%\PrusaSlicer\filament\*.ini
%APPDATA%\PrusaSlicer\vendor\*.ini
```

Point it at another PrusaSlicer configuration folder:

```powershell
dotnet PresetConverter\bin\Debug\net10.0\PresetConverter.dll --prusa-config-dir "D:\Profiles\PrusaSlicer"
```

Or provide explicit inputs:

```powershell
dotnet PresetConverter\bin\Debug\net10.0\PresetConverter.dll --input "C:\Presets\Prusament PLA.ini"
```

By default, output is written directly to an `Output` folder beside the application:

```text
PresetConverter\bin\Debug\net10.0\Output
```

Write directly to a folder for inspection:

```powershell
dotnet PresetConverter\bin\Debug\net10.0\PresetConverter.dll --input "$env:APPDATA\PrusaSlicer\vendor\PrusaResearch.ini" --outdir "C:\Temp\OrcaConverted" --force-output --on-existing overwrite
```

## Useful Inputs

Custom PrusaSlicer filament presets are usually stored here:

```powershell
$env:APPDATA\PrusaSlicer\filament\*.ini
```

Built-in/vendor presets are usually config bundles here:

```powershell
$env:APPDATA\PrusaSlicer\vendor\*.ini
```

## Privacy And Safety

Slicer presets can contain custom G-code, file paths, notes, vendor names, printer names, and other personal or machine-specific details. Review input presets before sharing them in issues or examples, and review generated OrcaSlicer presets before using them for real prints.

## Configuration File

The console host uses the standard .NET hosting/configuration pipeline. Settings can be supplied in `appsettings.json` beside the executable. Host settings live under `PresetConverter`, Prusa reader settings under `Prusa`, and Orca writer settings under `Orca`. Command-line values override file settings where supported.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "PresetConverter": {
    "Inputs": [],
    "OutputDirectory": null,
    "OnExisting": "Skip",
    "ForceOutputDirectory": true
  },
  "Prusa": {
    "ConfigurationDirectory": "%APPDATA%\\PrusaSlicer",
    "InputPatterns": [
      "filament\\*.ini",
      "vendor\\*.ini"
    ],
    "SkipIncompletePresets": true,
    "PresetIdPattern": "^[^@]+$"
  },
  "Orca": {
    "PrinterSpecificGCode": "RemoveBrandBlocks",
    "NozzleSize": null,
    "OrcaSlicerVersion": "1.6.0.0"
  }
}
```

## Options

```text
--input, -i <file|directory|pattern>
--prusa-config-dir <directory>
--preset-id-pattern <regex>
--outdir, -o <directory>
--force-output
--on-existing <skip|overwrite|merge>
--printer-specific-gcode <remove-brand-blocks|remove-all|keep-all>
--nozzle-size <mm>
```

Defaults:

```text
--on-existing skip
--printer-specific-gcode remove-brand-blocks
```

The default preset id pattern is `^[^@]+$`, which keeps base filament presets such as `Prusament PLA.json` and skips device-specific variants such as `Prusament PLA @MK4.json`.
Use `--preset-id-pattern ".*@MK4S.*"` to export only MK4S-specific variants, or `--preset-id-pattern ".*"` to export everything.

When all default Prusa inputs are used, the converter reads all selected presets before writing output. It uses `Templates.ini` only for generic material type presets such as `PLA.json`, `PETG.json`, `ABS.json`, and `TPU.json`. Branded filament presets such as `Prusa PLA.json` or `Fillamentum PLA.json` are kept even when their settings match the generic material type. Machine and nozzle variants are written only when their canonical filament settings differ from the nearest less-specific preset.

When `OutputDirectory` is not configured, the console host writes to an `Output` directory beside the application. `ForceOutputDirectory` is enabled by default so the generated Orca `.json` files appear directly in that folder.

`remove-brand-blocks` keeps generic filament G-code while removing conditional blocks that reference Prusa printer vendor/model markers. Empty G-code and comment-only G-code fields are omitted.

## Maintenance And Forks

This source is shared for anyone who finds it useful, but it is not intended to be an actively maintained upstream project. Forks are welcome, and downstream maintainers should feel free to adapt the code, change direction, or publish their own continued version under the license terms.

If you share examples publicly, remove personal paths, names, and custom G-code that should not be public.

## License

PresetConverter is licensed under the MIT License. See [LICENSE](LICENSE).
