using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Services;
using MangaMesh.Shared.Configuration;

namespace MangaMesh.Peer.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMangaMeshDhtNode(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DhtOptions>(configuration.GetSection("Dht"));
            services.Configure<WebRtcOptions>(configuration.GetSection("WebRtc"));

            services.TryAddSingleton<ITransport>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var port = config.GetValue<int>("Dht:Port", 3001);
                return new TcpTransport(listenPort: port);
            });

            services.AddSingleton<IDhtStorage, InMemoryDhtStorage>();
            services.AddSingleton<IBootstrapNodeProvider, YamlBootstrapNodeProvider>();

            services.AddSingleton<IRoutingTable>(sp =>
                new KBucketRoutingTable(sp.GetRequiredService<INodeIdentity>().NodeId));

            services.AddSingleton<IDhtRequestTracker, DhtRequestTracker>();

            services.AddSingleton<ITransportSelector>(sp =>
                         new AlwaysTcpTransportSelector(sp.GetRequiredService<ITransport>()));

            services.AddSingleton<IDhtNode>(sp =>
                new DhtNode(
                    sp.GetRequiredService<INodeIdentity>(),
                    sp.GetRequiredService<ITransport>(),
                    sp.GetRequiredService<IDhtStorage>(),
                    sp.GetRequiredService<IRoutingTable>(),
                    sp.GetRequiredService<IBootstrapNodeProvider>(),
                    sp.GetRequiredService<IDhtRequestTracker>(),
                    sp.GetRequiredService<IKeyPairService>(),
                    sp.GetRequiredService<IKeyStore>(),
                    sp.GetRequiredService<INodeConnectionInfoProvider>(),
                    sp.GetRequiredService<ILogger<DhtNode>>(),
                    sp.GetRequiredService<ITransportSelector>(),
                    sp.GetService<Microsoft.Extensions.Options.IOptions<WebRtcOptions>>()));

            services.AddSingleton<IDhtMaintenanceService>(sp =>
                new DhtMaintenanceService(
                    sp.GetRequiredService<IDhtNode>(),
                    sp.GetRequiredService<IRoutingTable>(),
                    sp.GetRequiredService<IDhtStorage>(),
                    sp.GetRequiredService<INodeAnnouncer>(),
                    sp.GetRequiredService<INodeIdentity>(),
                    sp.GetRequiredService<ILogger<DhtMaintenanceService>>(),
                    sp.GetService<IManifestStore>(),
                    sp.GetService<INodeIdentityService>()));

            services.AddSingleton<DhtProtocolHandler>();
            services.AddSingleton<ContentProtocolHandler>();
            
            services.AddSingleton<ProtocolRouter>(sp =>
            {
                var router = new ProtocolRouter();
                router.Register(sp.GetRequiredService<DhtProtocolHandler>());
                router.Register(sp.GetRequiredService<ContentProtocolHandler>());
                return router;
            });

            return services;
        }

        public static IServiceCollection AddMangaMeshChapterServices(this IServiceCollection services)
        {
            services.AddSingleton<IImageFormatProvider, DefaultImageFormatProvider>();
            services.AddSingleton<IChapterSourceReader, DirectorySourceReader>();
            services.AddSingleton<IChapterSourceReader, ZipSourceReader>();
            services.AddSingleton<IManifestSigningService, ManifestSigningService>();
            
            services.AddScoped<IChunkIngester, ChunkIngester>();
            services.AddScoped<IChapterIngestionService, ChapterIngestionService>();
            services.AddScoped<IChapterPublisherService, ChapterPublisherService>();

            return services;
        }
    }
}
