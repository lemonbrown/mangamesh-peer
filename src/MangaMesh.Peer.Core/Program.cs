// See https://aka.ms/new-console-template for more information
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Data;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Metadata;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

Console.WriteLine("                                                   _     \r\n  /\\/\\   __ _ _ __   __ _  __ _    /\\/\\   ___  ___| |__  \r\n /    \\ / _` | '_ \\ / _` |/ _` |  /    \\ / _ \\/ __| '_ \\ \r\n/ /\\/\\ \\ (_| | | | | (_| | (_| | / /\\/\\ \\  __/\\__ \\ | | |\r\n\\/    \\/\\__,_|_| |_|\\__, |\\__,_| \\/    \\/\\___||___/_| |_|\r\n                    |___/                                ");

var builder = new HostBuilder()
    .ConfigureAppConfiguration(c => c.AddEnvironmentVariables().AddCommandLine(args))
    .ConfigureServices((context, services) =>
{
    var config = context.Configuration;
    var trackerUrl = config["TrackerUrl"] ?? "https://localhost:7030";

    services.Configure<BlobStoreOptions>(config.GetSection("BlobStore"));
    services.Configure<ManifestStoreOptions>(config.GetSection("ManifestStore"));
    services.Configure<DhtOptions>(config.GetSection("Dht"));

    services
    .AddLogging(n => n.AddConsole())
    .AddScoped<ITrackerClient, TrackerClient>()
    .AddScoped<IPeerFetcher, PeerFetcher>()
    .AddSingleton<IManifestStore, ManifestStore>()
    .AddSingleton<IStorageMonitorService, StorageMonitorService>()
    .AddSingleton<IBlobStore, BlobStore>()
    .AddDbContext<ClientDbContext>(options =>
        options.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "data", "mangamesh.db")}"))
    .AddSingleton<IKeyStore, SqliteKeyStore>()
    .AddSingleton<INodeIdentityService, NodeIdentityService>()
    .AddSingleton<IKeyPairService, KeyPairService>()
    .AddScoped<ITrackerPublisher, TrackerPublisher>()
    .AddSingleton<IImageFormatProvider, DefaultImageFormatProvider>()
    .AddSingleton<IChapterSourceReader, DirectorySourceReader>()
    .AddSingleton<IChapterSourceReader, ZipSourceReader>()
    .AddSingleton<IManifestSigningService, ManifestSigningService>();

    services.AddHttpClient<IMetadataClient, HttpMetadataClient>(client =>
    {
        client.BaseAddress = new Uri("https://metadata.mangamesh.net");
    });

    services.AddHttpClient<ITrackerClient, TrackerClient>(client =>
    {
        client.BaseAddress = new Uri(trackerUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        // Only bypass certificate validation when explicitly enabled (e.g. local dev with self-signed certs).
        // Set MANGAMESH_SKIP_TLS_VERIFY=true in the environment to enable; never set this in production.
        var skipTls = string.Equals(
            Environment.GetEnvironmentVariable("MANGAMESH_SKIP_TLS_VERIFY"), "true",
            StringComparison.OrdinalIgnoreCase);
        if (skipTls)
        {
            Console.WriteLine("[WARN] Peer.Core - TLS certificate validation is DISABLED (MANGAMESH_SKIP_TLS_VERIFY=true). Do not use in production.");
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return handler;
    });

    // Forward narrower interfaces to the same TrackerClient instance
    services.AddTransient<IPeerLocator>(sp => (IPeerLocator)sp.GetRequiredService<ITrackerClient>());
    services.AddTransient<INodeAnnouncer>(sp => (INodeAnnouncer)sp.GetRequiredService<ITrackerClient>());
    services.AddTransient<ISeriesRegistry>(sp => (ISeriesRegistry)sp.GetRequiredService<ITrackerClient>());
    services.AddTransient<IManifestAnnouncer>(sp => (IManifestAnnouncer)sp.GetRequiredService<ITrackerClient>());
    services.AddTransient<ITrackerChallengeClient>(sp => (ITrackerChallengeClient)sp.GetRequiredService<ITrackerClient>());

    services.AddSingleton<INodeConnectionInfoProvider, ConsoleNodeConnectionInfoProvider>();

    // ======================================================
    // Node identity (singleton, generates or loads keys)
    // ======================================================
    services.AddSingleton<INodeIdentity, NodeIdentity>();

    // ======================================================
    // Transport (singleton)
    // ======================================================
    services.AddSingleton<ITransport>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var port = config.GetValue<int>("Dht:Port", 3001);
        return new TcpTransport(listenPort: port, sp.GetRequiredService<ILogger<TcpTransport>>());
    });

    // ======================================================
    // Storage (singleton)
    // ======================================================
    services.AddSingleton<IDhtStorage, InMemoryDhtStorage>();

    // ======================================================
    // DHT node (singleton)
    // ======================================================
    services.AddSingleton<IBootstrapNodeProvider, YamlBootstrapNodeProvider>();

    // Protocol Handlers — ProtocolRouter and DhtProtocolHandler get loggers via DI
    services.AddSingleton<ProtocolRouter>();
    services.AddSingleton<DhtProtocolHandler>();
    services.AddSingleton<ContentProtocolHandler>();
    services.AddSingleton<IProtocolHandler>(sp => sp.GetRequiredService<DhtProtocolHandler>());
    services.AddSingleton<IProtocolHandler>(sp => sp.GetRequiredService<ContentProtocolHandler>());

    services.AddSingleton<IDhtNode>(sp =>
    {
        var startupLogger = sp.GetRequiredService<ILogger<DhtNode>>();
        startupLogger.LogDebug("Resolving IDhtNode...");
        var identity = sp.GetRequiredService<INodeIdentity>();
        var transport = sp.GetRequiredService<ITransport>();
        var storage = sp.GetRequiredService<IDhtStorage>();
        var keyStore = sp.GetRequiredService<IKeyStore>();
        var keypairService = sp.GetRequiredService<IKeyPairService>();
        var tracker = sp.GetRequiredService<INodeAnnouncer>();
        var connectionInfo = sp.GetRequiredService<INodeConnectionInfoProvider>();
        var bootstrapProvider = sp.GetRequiredService<IBootstrapNodeProvider>();
        var manifestStore = sp.GetService<IManifestStore>();
        var routingTable = new KBucketRoutingTable(identity.NodeId);
        var requestTracker = new DhtRequestTracker();

        // Wire up protocol handlers
        var router = sp.GetRequiredService<ProtocolRouter>();
        var dhtHandler = sp.GetRequiredService<DhtProtocolHandler>();
        var contentHandler = sp.GetRequiredService<ContentProtocolHandler>();

        router.Register(dhtHandler);
        router.Register(contentHandler);

        transport.OnMessage += router.RouteAsync;

        var node = new DhtNode(identity, transport, storage, routingTable, bootstrapProvider, requestTracker, keypairService, keyStore, connectionInfo, startupLogger);

        // Circular dependency resolution for ContentProtocolHandler -> DhtNode (for request tracking/callbacks)
        contentHandler.DhtNode = node;
        dhtHandler.DhtNode = node;

        startupLogger.LogDebug("IDhtNode resolved");
        return node;
    });

    services.AddSingleton<IDhtMaintenanceService>(sp =>
    {
        var dhtNode = sp.GetRequiredService<IDhtNode>();
        var identity = sp.GetRequiredService<INodeIdentity>();
        var storage = sp.GetRequiredService<IDhtStorage>();
        var tracker = sp.GetRequiredService<INodeAnnouncer>();
        var routingTable = new KBucketRoutingTable(identity.NodeId);
        var manifestStore = sp.GetService<IManifestStore>();
        var maintenanceLogger = sp.GetRequiredService<ILogger<DhtMaintenanceService>>();
        var identityService = sp.GetService<INodeIdentityService>();
        return new DhtMaintenanceService(dhtNode, routingTable, storage, tracker, identity, maintenanceLogger, manifestStore, identityService);
    });

    // Hosted service
    services.AddHostedService<DhtHostedService>();

    //services.AddHostedService(provider =>
    //    new ReplicationService(
    //        scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
    //        logger: provider.GetRequiredService<ILogger<ReplicationService>>(),
    //        nodeIdentity: provider.GetRequiredService<INodeIdentityService>(),
    //        connectionInfo: provider.GetRequiredService<INodeConnectionInfoProvider>()
    //    )
    //);
});

var host = builder.Build();
var hostLogger = host.Services.GetRequiredService<ILogger<Program>>();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
    db.Database.EnsureCreated();

    // Schema Patch: Ensure Manifests table exists (EnsureCreated doesn't migrate existing DBs)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Manifests"" (
            ""Hash"" TEXT NOT NULL CONSTRAINT ""PK_Manifests"" PRIMARY KEY,
            ""SeriesId"" TEXT NOT NULL,
            ""ChapterId"" TEXT NOT NULL,
            ""DataJson"" TEXT NOT NULL,
            ""CreatedUtc"" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_Manifests_SeriesId"" ON ""Manifests"" (""SeriesId"");
        CREATE INDEX IF NOT EXISTS ""IX_Manifests_ChapterId"" ON ""Manifests"" (""ChapterId"");
    ");

    // Migration: specific logic to move keys from JSON to SQLite
    if (!db.Keys.Any())
    {
        var jsonKeyPath = Path.Combine(AppContext.BaseDirectory, "data", "keys", "keys.json");
        if (File.Exists(jsonKeyPath))
        {
            try
            {
                var json = File.ReadAllText(jsonKeyPath);
                var keyPair = System.Text.Json.JsonSerializer.Deserialize<PublicPrivateKeyPair>(json);
                if (keyPair != null)
                {
                    db.Keys.Add(new KeyEntity
                    {
                        PublicKey = keyPair.PublicKeyBase64,
                        PrivateKey = keyPair.PrivateKeyBase64,
                        CreatedAt = DateTime.UtcNow
                    });
                    db.SaveChanges();
                    hostLogger.LogInformation("Migrated keys from JSON to SQLite");
                }
            }
            catch (Exception ex)
            {
                hostLogger.LogError(ex, "Failed to migrate keys from JSON");
            }
        }
    }

    // Migration: Manifests
    if (!db.Manifests.Any())
    {
        var manifestDir = Path.Combine(AppContext.BaseDirectory, "input", "manifests");
        if (Directory.Exists(manifestDir))
        {
            var files = Directory.GetFiles(manifestDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var hash = Path.GetFileNameWithoutExtension(file);
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<MangaMesh.Shared.Models.ChapterManifest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (manifest != null)
                    {
                        db.Manifests.Add(new ManifestEntity
                        {
                            Hash = hash,
                            SeriesId = manifest.SeriesId,
                            ChapterId = manifest.ChapterId,
                            DataJson = json, // Keep original JSON strictly
                            CreatedUtc = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    hostLogger.LogWarning(ex, "Failed to migrate manifest {File}", file);
                }
            }
            if (files.Length > 0)
            {
                db.SaveChanges();
                hostLogger.LogInformation("Migrated {Count} manifests to SQLite", files.Length);
            }
        }
    }
}

hostLogger.LogInformation("Starting MangaMesh peer node");
await host.RunAsync();
hostLogger.LogInformation("MangaMesh peer node stopped");

Console.ReadLine();