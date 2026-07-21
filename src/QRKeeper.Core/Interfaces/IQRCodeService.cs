using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface IQRCodeService
{
    Task<string?> DecodeAsync(Stream imageStream, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QRCodeDecodeResult>> DecodeAllAsync(Stream imageStream, CancellationToken cancellationToken = default);

    byte[] GeneratePng(string content);
}
