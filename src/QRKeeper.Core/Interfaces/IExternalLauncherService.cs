namespace QRKeeper.Core.Interfaces;

public interface IExternalLauncherService
{
    Task OpenUriAsync(string uri, CancellationToken cancellationToken = default);
}
