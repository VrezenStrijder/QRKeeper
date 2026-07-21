using Android.Content;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.Android.Services;

public sealed class AndroidExternalLauncherService : IExternalLauncherService
{
    private readonly Func<MainActivity?> getActivity;

    public AndroidExternalLauncherService(Func<MainActivity?> getActivity)
    {
        this.getActivity = getActivity;
    }

    public Task OpenUriAsync(string uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MainActivity activity = getActivity()
            ?? throw new InvalidOperationException("Android activity is not available.");
        Intent intent = new(Intent.ActionView, global::Android.Net.Uri.Parse(uri));
        activity.StartActivity(intent);
        return Task.CompletedTask;
    }
}
