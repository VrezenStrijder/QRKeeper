using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRKeeper.Core.Interfaces;
using QRKeeper.Infrastructure.Data;
using QRKeeper.Infrastructure.DependencyInjection;
using QRKeeper.UI.Services;
using QRKeeper.UI.ViewModels;
using QRKeeper.UI.Views;

namespace QRKeeper.UI;

public partial class App : Application
{
    private static readonly TimeSpan SyncHostStopTimeout = TimeSpan.FromSeconds(3); // Bounds shutdown wait after the window closes.
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = new();
            _serviceProvider = BuildServices(mainWindow);
            DesktopSettingsService settingsService = _serviceProvider.GetRequiredService<DesktopSettingsService>();
            ApplySavedTheme(settingsService.Theme);
            _serviceProvider.GetRequiredService<ILocalizationService>().Apply(
                settingsService.Language);
            VisualStyleService.Apply(settingsService.ColorStyle, GetThemeVariant(settingsService.Theme));
            using (IServiceScope scope = _serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<AppDbContext>().EnsureCreatedAndMigrated();
            }

            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => StopSyncHost();
            StartSyncHost();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices(MainWindow mainWindow)
    {
        ServiceCollection services = new();
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QRKeeper");

        services.AddQRKeeperInfrastructure(appDataDirectory);
        services.AddSingleton(new DesktopSettingsService(appDataDirectory));
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IConfirmationService>(_ => new DialogConfirmationService(() => mainWindow));
        services.AddSingleton<IExternalLauncherService, DesktopExternalLauncherService>();
        services.AddSingleton<ISyncNetworkInfoService, DesktopSyncNetworkInfoService>();
        services.AddSingleton<ISyncLocalDeviceProvider, DesktopSyncLocalDeviceProvider>();
        services.AddSingleton<ISyncIncomingRequestPolicy, DesktopSyncIncomingRequestPolicy>();
        services.AddSingleton<IFilePickerService>(_ => new DesktopFilePickerService(() => mainWindow));
        services.AddSingleton<IScreenCaptureService>(provider =>
            new DesktopScreenCaptureService(
                () => mainWindow,
                provider.GetRequiredService<IQRCodeService>(),
                provider.GetRequiredService<ILocalizationService>()));
        services.AddTransient<HomeViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<SyncViewModel>();
        services.AddTransient<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }

    private void StartSyncHost()
    {
        if (_serviceProvider is null)
        {
            return;
        }

        try
        {
            _serviceProvider.GetRequiredService<ISyncHostService>().StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _serviceProvider.GetRequiredService<IMessageService>().Show(
                _serviceProvider.GetRequiredService<ILocalizationService>().GetString("Toast_SyncStartFailed"),
                ex.Message,
                MessageSeverity.Warning);
        }
    }

    private void StopSyncHost()
    {
        ServiceProvider? serviceProvider = _serviceProvider;
        _serviceProvider = null;
        if (serviceProvider is null)
        {
            return;
        }

        _ = Task.Run(() => StopAndDisposeServicesAsync(serviceProvider));
    }

    private static async Task StopAndDisposeServicesAsync(ServiceProvider serviceProvider)
    {
        try
        {
            using CancellationTokenSource cancellationTokenSource = new(SyncHostStopTimeout);
            await serviceProvider.GetRequiredService<ISyncHostService>()
                .StopAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        try
        {
            await serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static void ApplySavedTheme(ThemeMode theme)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = theme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static ThemeVariant GetThemeVariant(ThemeMode theme)
    {
        return theme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => Current?.ActualThemeVariant ?? ThemeVariant.Default
        };
    }
}
