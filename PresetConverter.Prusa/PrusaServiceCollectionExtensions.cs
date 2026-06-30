using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PresetConverter;

public static class PrusaServiceCollectionExtensions
{
    public static IServiceCollection AddPrusa(this IServiceCollection services, Action<PrusaPresetReaderOptions>? configure = null)
    {
        services.AddOptions<PrusaPresetReaderOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPresetReader, PrusaPresetReader>());
        return services;
    }
}