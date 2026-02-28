using MangaMesh.Shared.Stores;
using MangaMesh.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMangaMeshSharedStores(this IServiceCollection services)
        {
            services.AddScoped<IManifestEntryStore, SqliteManifestEntryStore>();
            services.AddScoped<IManifestAnnouncerStore, SqliteManifestAnnouncerStore>();
            services.AddScoped<ISeriesDerivationService, SeriesDerivationService>();
            
            services.AddScoped<IPublicKeyStore, SqlitePublicKeyStore>();
            services.AddScoped<IApprovedKeyStore, SqliteApprovedKeyStore>();
            services.AddScoped<IChallengeStore, SqliteChallengeStore>();
            services.AddScoped<IFlagStore, SqliteFlagStore>();

            return services;
        }
    }
}
