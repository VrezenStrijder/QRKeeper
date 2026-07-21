using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRKeeper.Android.Services;
using QRKeeper.Android.ViewModels;
using QRKeeper.Android.Views;
using QRKeeper.Core.Interfaces;
using QRKeeper.Infrastructure.Data;
using QRKeeper.Infrastructure.DependencyInjection;

namespace QRKeeper.Android;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;
    private MainView? _mainView;
    private readonly object syncHostLifecycleGate = new();
    private Task syncHostLifecycleTask = Task.CompletedTask;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            ServiceProvider serviceProvider = EnsureServices();

            _mainView = new MainView
            {
                DataContext = serviceProvider.GetRequiredService<MainViewModel>()
            };
            singleView.MainView = _mainView;
            StartSyncHost();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void StartSyncHost()
    {
        QueueSyncHostLifecycle(async serviceProvider =>
        {
            AndroidSyncNetworkService networkService =
                serviceProvider.GetRequiredService<AndroidSyncNetworkService>();
            try
            {
                networkService.AcquireMulticastLock();
                await serviceProvider.GetRequiredService<ISyncHostService>().StartAsync();
            }
            catch (Exception)
            {
                networkService.ReleaseMulticastLock();
            }
        });
    }

    public void StopSyncHost()
    {
        QueueSyncHostLifecycle(async serviceProvider =>
        {
            AndroidSyncNetworkService networkService =
                serviceProvider.GetRequiredService<AndroidSyncNetworkService>();
            try
            {
                await serviceProvider.GetRequiredService<ISyncHostService>().StopAsync();
            }
            finally
            {
                networkService.ReleaseMulticastLock();
            }
        });
    }

    public string GetAndroidText(string key, string fallback)
    {
        if (_serviceProvider is null)
        {
            return fallback;
        }

        AndroidSettingsService settingsService = _serviceProvider.GetRequiredService<AndroidSettingsService>();
        AndroidTextService textService = _serviceProvider.GetRequiredService<AndroidTextService>();
        string text = textService.Get(settingsService.Language, key);
        return string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private ServiceProvider EnsureServices()
    {
        if (_serviceProvider is not null)
        {
            return _serviceProvider;
        }

        _serviceProvider = BuildServices();
        AndroidSettingsService settingsService = _serviceProvider.GetRequiredService<AndroidSettingsService>();
        ApplySavedTheme(settingsService.Theme);
        AndroidVisualStyleService.Apply(settingsService.ColorStyle, GetThemeVariant(settingsService.Theme));
        using IServiceScope scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().EnsureCreatedAndMigrated();
        return _serviceProvider;
    }

    private void QueueSyncHostLifecycle(Func<ServiceProvider, Task> action)
    {
        ServiceProvider? serviceProvider = _serviceProvider;
        if (serviceProvider is null)
        {
            return;
        }

        lock (syncHostLifecycleGate)
        {
            syncHostLifecycleTask = syncHostLifecycleTask.ContinueWith(
                _ => RunSyncHostLifecycleActionAsync(serviceProvider, action),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();
        }
    }

    private static async Task RunSyncHostLifecycleActionAsync(
        ServiceProvider serviceProvider,
        Func<ServiceProvider, Task> action)
    {
        try
        {
            await action(serviceProvider);
        }
        catch (Exception)
        {
        }
    }

    private ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        string appDataDirectory = Path.Combine(
            global::Android.App.Application.Context.FilesDir?.AbsolutePath
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QRKeeper");

        services.AddQRKeeperInfrastructure(appDataDirectory);
        services.AddSingleton(new AndroidSettingsService(appDataDirectory));
        services.AddSingleton<AndroidTextService>();
        services.AddSingleton<AndroidSyncNetworkService>();
        services.AddSingleton<ISyncNetworkInfoService>(provider =>
            provider.GetRequiredService<AndroidSyncNetworkService>());
        services.AddSingleton<ISyncLocalDeviceProvider, AndroidSyncLocalDeviceProvider>();
        services.AddSingleton<ISyncIncomingRequestPolicy, AndroidSyncIncomingRequestPolicy>();
        services.AddSingleton(_ => new AndroidBackupFileService(
            () => _mainView is null ? null : TopLevel.GetTopLevel(_mainView),
            appDataDirectory));
        services.AddSingleton(_ => new AndroidQrImageShareService(
            () => MainActivity.Current,
            () => _mainView is null ? null : TopLevel.GetTopLevel(_mainView)));
        services.AddSingleton<IExternalLauncherService>(_ => new AndroidExternalLauncherService(() => MainActivity.Current));
        services.AddSingleton<AndroidDialogService>();
        services.AddSingleton<IFilePickerService>(_ => new AndroidFilePickerService(
            () => _mainView is null ? null : TopLevel.GetTopLevel(_mainView)));
        services.AddSingleton<ICameraService>(_ => new AndroidCameraService(() => MainActivity.Current));
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }

    private static void ApplySavedTheme(AndroidThemeMode theme)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = theme switch
        {
            AndroidThemeMode.Light => ThemeVariant.Light,
            AndroidThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static ThemeVariant GetThemeVariant(AndroidThemeMode theme)
    {
        return theme switch
        {
            AndroidThemeMode.Light => ThemeVariant.Light,
            AndroidThemeMode.Dark => ThemeVariant.Dark,
            _ => Current?.ActualThemeVariant ?? ThemeVariant.Default
        };
    }
}
