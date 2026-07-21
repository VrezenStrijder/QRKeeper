using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.Services;

public sealed class AndroidCameraService : ICameraService
{
    private readonly Func<MainActivity?> _getActivity;

    public AndroidCameraService(Func<MainActivity?> getActivity)
    {
        _getActivity = getActivity;
    }

    public bool IsSupported => _getActivity() is not null;

    public async Task<ScanResult?> ScanAsync(CancellationToken cancellationToken = default)
    {
        MainActivity? activity = _getActivity();
        if (activity is null)
        {
            throw new InvalidOperationException("Camera is not available.");
        }

        string? content = await activity.ScanQrAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(content)
            ? null
            : new ScanResult
            {
                Content = content,
                Source = QRRecordSource.Scan
            };
    }
}
