using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRKeeper.Core.Interfaces;
using QRKeeper.Infrastructure.Data;
using QRKeeper.Infrastructure.Repositories;
using QRKeeper.Infrastructure.Services;

namespace QRKeeper.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQRKeeperInfrastructure(
        this IServiceCollection services,
        string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        string databasePath = Path.Combine(appDataDirectory, "qrkeeper.db");

        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<IQRRecordRepository, QRRecordRepository>();
        services.AddSingleton<IContentTypeDetector, ContentTypeDetector>();
        services.AddSingleton<IQRCodeService, QRCodeService>();
        services.AddSingleton<IImageStorageService>(_ => new ImageStorageService(appDataDirectory));
        services.AddSingleton<IUpdateService>(_ => new GitHubUpdateService(new HttpClient()));
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ISyncMergeService, SyncMergeService>();
        services.AddSingleton<SyncMessageSerializer>();
        services.AddScoped<ISyncRequestHandler, SyncRequestHandler>();
        services.AddSingleton<ISyncIncomingRequestPolicy, AllowAllSyncIncomingRequestPolicy>();
        services.AddSingleton<ISyncTransportService, TcpSyncTransportService>();
        services.AddSingleton<ISyncListenerService, TcpSyncListenerService>();
        services.AddSingleton<ISyncDiscoveryService, UdpBroadcastSyncDiscoveryService>();
        services.AddSingleton<ISyncHostService, SyncHostService>();
        services.AddScoped<ISyncCoordinator, SyncCoordinator>();

        return services;
    }
}
