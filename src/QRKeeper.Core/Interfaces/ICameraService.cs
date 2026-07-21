using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface ICameraService
{
    bool IsSupported { get; }

    Task<ScanResult?> ScanAsync(CancellationToken cancellationToken = default);
}
