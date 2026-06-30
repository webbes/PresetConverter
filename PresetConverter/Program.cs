using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PresetConverter;

var exitCode = 0;

try
{
    var commandLine = CommandLineOptions.Parse(args);
    if (commandLine.ShowHelp)
    {
        CommandLineOptions.PrintUsage();
        return Exit(0);
    }

    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    builder.Logging.ClearProviders();
    builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });

    builder.Services.Configure<ConverterHostOptions>(builder.Configuration.GetSection("PresetConverter"));
    builder.Services.PostConfigure<ConverterHostOptions>(commandLine.ApplyTo);
    builder.Services.Configure<PrusaPresetReaderOptions>(builder.Configuration.GetSection("Prusa"));
    builder.Services.PostConfigure<PrusaPresetReaderOptions>(options =>
    {
        if (options.InputPatterns.Length == 0)
        {
            options.InputPatterns = PrusaPresetReaderOptions.CreateDefaultInputPatterns();
        }
    });
    builder.Services.PostConfigure<PrusaPresetReaderOptions>(commandLine.ApplyTo);
    builder.Services.Configure<OrcaPresetWriterOptions>(builder.Configuration.GetSection("Orca"));
    builder.Services.PostConfigure<OrcaPresetWriterOptions>(commandLine.ApplyTo);
    builder.Services.AddPrusa();
    builder.Services.AddOrca();
    builder.Services.AddSingleton<FilamentPresetSelector>();
    builder.Services.AddSingleton<ConversionApplication>();

    using var host = builder.Build();
    var options = host.Services.GetRequiredService<IOptions<ConverterHostOptions>>().Value;
    var application = host.Services.GetRequiredService<ConversionApplication>();
    var results = application.Run(options).ToArray();

    foreach (var result in results)
    {
        var action = result.Status.ToString().ToUpperInvariant();
        Console.WriteLine($"{action}: {result.SourceName} -> {result.OutputPath ?? string.Join("; ", result.Warnings.Select(warning => warning.Message))}");
    }

    PrintSummary(results);
    exitCode = results.Any(result => result.Status == ConversionItemStatus.Failed) ? 1 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    exitCode = 1;
}

return Exit(exitCode);

static int Exit(int exitCode)
{
    WaitForKeyIfInteractive();
    return exitCode;
}

static void PrintSummary(IReadOnlyCollection<ConversionItemResult> results)
{
    Console.WriteLine();
    Console.WriteLine("Result overview");
    Console.WriteLine($"  Succeeded: {results.Count(result => result.Status == ConversionItemStatus.Succeeded)}");
    Console.WriteLine($"  Skipped:   {results.Count(result => result.Status == ConversionItemStatus.Skipped)}");
    Console.WriteLine($"  Failed:    {results.Count(result => result.Status == ConversionItemStatus.Failed)}");
    Console.WriteLine($"  Total:     {results.Count}");
}

static void WaitForKeyIfInteractive()
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        return;
    }

    Console.WriteLine();
    Console.Write("Press any key to close this window...");
    Console.ReadKey(intercept: true);
    Console.WriteLine();
}
