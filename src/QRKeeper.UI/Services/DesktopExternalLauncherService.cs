using System.Diagnostics;
using QRKeeper.Core.Interfaces;

namespace QRKeeper.UI.Services;

public sealed class DesktopExternalLauncherService : IExternalLauncherService
{
    public Task OpenUriAsync(string uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo(uri)
        {
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }
}
