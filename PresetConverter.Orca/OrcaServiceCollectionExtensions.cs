using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PresetConverter;

public static class OrcaServiceCollectionExtensions
{
    public static IServiceCollection AddOrca(this IServiceCollection services, Action<OrcaPresetWriterOptions>? configure = null)
    {
        services.AddOptions<OrcaPresetWriterOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPresetWriter, OrcaPresetWriter>());
        return services;
    }
}