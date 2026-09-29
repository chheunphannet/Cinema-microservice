using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Foundation.Storage;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddCinemaStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var storageSection = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(storageSection);

        var provider = storageSection["Provider"]?.Trim();

        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageService, S3StorageService>();
        }
        else
        {
            services.AddSingleton<IStorageService, LocalStorageService>();
        }

        return services;
    }
}
