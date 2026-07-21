using Android.App;
using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using QRKeeper.Android.ViewModels;

namespace QRKeeper.Android;

[Activity(
    Label = "QRKeeper",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    [Intent.ActionSend],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "image/*")]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const int CameraScanRequestCode = 4101;
    private const int NetworkNamePermissionRequestCode = 4102;
    private const string NearbyWifiDevicesPermission = "android.permission.NEARBY_WIFI_DEVICES";
    private TaskCompletionSource<string?>? _cameraScanCompletion;
    private TaskCompletionSource<bool>? networkNamePermissionCompletion;
    private global::Android.Net.Uri? pendingSharedImageUri;
    private bool isStartingCameraScan;

    public static MainActivity? Current { get; private set; }

    public event Action<global::Android.Net.Uri>? SharedImageReceived;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }

    protected override void OnStart()
    {
        base.OnStart();
        Current = this;
        TryImportSharedImage(Intent);
        (Avalonia.Application.Current as App)?.StartSyncHost();
    }

    protected override void OnStop()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        if (!isStartingCameraScan)
        {
            (Avalonia.Application.Current as App)?.StopSyncHost();
        }

        base.OnStop();
    }

    public Task<string?> ScanQrAsync()
    {
        if (_cameraScanCompletion is not null)
        {
            return Task.FromResult<string?>(null);
        }

        Intent intent = new(this, typeof(CameraScanActivity));
        intent.PutExtra(
            CameraScanActivity.PromptTextExtra,
            GetAndroidText("ScanCameraPrompt", "Point the camera at a QR code."));
        intent.PutExtra(
            CameraScanActivity.ScanningTextExtra,
            GetAndroidText("ScanCameraScanning", "Scanning..."));
        intent.PutExtra(
            CameraScanActivity.CancelTextExtra,
            GetAndroidText("Cancel", "Cancel"));
        _cameraScanCompletion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        isStartingCameraScan = true;
#pragma warning disable CS0618
        try
        {
            StartActivityForResult(intent, CameraScanRequestCode);
        }
        catch
        {
            isStartingCameraScan = false;
            _cameraScanCompletion = null;
            throw;
        }
#pragma warning restore CS0618
        return _cameraScanCompletion.Task;
    }

    public Task<bool> EnsureWifiNetworkNamePermissionAsync()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            return Task.FromResult(true);
        }

        if (HasWifiNetworkNamePermissions())
        {
            return Task.FromResult(true);
        }

        List<string> permissions = new();
        if (CheckSelfPermission(Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
        {
            permissions.Add(Manifest.Permission.AccessCoarseLocation);
        }

        if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) != Permission.Granted)
        {
            permissions.Add(Manifest.Permission.AccessFineLocation);
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(NearbyWifiDevicesPermission) != Permission.Granted)
        {
            permissions.Add(NearbyWifiDevicesPermission);
        }

        if (permissions.Count == 0)
        {
            return Task.FromResult(true);
        }

        if (networkNamePermissionCompletion is not null)
        {
            return networkNamePermissionCompletion.Task;
        }

        networkNamePermissionCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RequestPermissions(permissions.ToArray(), NetworkNamePermissionRequestCode);
        return networkNamePermissionCompletion.Task;
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        TryImportSharedImage(intent);
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != NetworkNamePermissionRequestCode)
        {
            return;
        }

        TaskCompletionSource<bool>? completion = networkNamePermissionCompletion;
        networkNamePermissionCompletion = null;
        completion?.TrySetResult(HasWifiNetworkNamePermissions());
    }

#pragma warning disable CS0618
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
#pragma warning restore CS0618

        if (requestCode != CameraScanRequestCode)
        {
            return;
        }

        isStartingCameraScan = false;
        TaskCompletionSource<string?>? completion = _cameraScanCompletion;
        _cameraScanCompletion = null;
        string? content = resultCode == Result.Ok
            ? data?.GetStringExtra(CameraScanActivity.ResultExtra)
            : null;
        completion?.TrySetResult(content);
    }

    public bool TryImportPendingSharedImage(MainViewModel viewModel)
    {
        if (pendingSharedImageUri is null)
        {
            return false;
        }

        global::Android.Net.Uri uri = pendingSharedImageUri;
        pendingSharedImageUri = null;
        _ = viewModel.ImportSharedImageAsync(uri);
        return true;
    }

    private void TryImportSharedImage(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend)
        {
            return;
        }

        global::Android.Net.Uri? uri = GetSharedImageUri(intent);
        if (uri is null)
        {
            return;
        }

        if (SharedImageReceived is not null)
        {
            SharedImageReceived.Invoke(uri);
            return;
        }

        pendingSharedImageUri = uri;
    }

    private static global::Android.Net.Uri? GetSharedImageUri(Intent intent)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416
            return intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(global::Android.Net.Uri)))
                as global::Android.Net.Uri;
#pragma warning restore CA1416
        }

#pragma warning disable CS0618, CA1422
        return intent.GetParcelableExtra(Intent.ExtraStream) as global::Android.Net.Uri;
#pragma warning restore CS0618, CA1422
    }

    private static string GetAndroidText(string key, string fallback)
    {
        return (Avalonia.Application.Current as App)?.GetAndroidText(key, fallback) ?? fallback;
    }

    private bool HasWifiNetworkNamePermissions()
    {
        bool hasLocationPermission =
            CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted;
        bool hasNearbyWifiPermission =
            Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu ||
            CheckSelfPermission(NearbyWifiDevicesPermission) == Permission.Granted;
        return hasLocationPermission && hasNearbyWifiPermission;
    }
}
