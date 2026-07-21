namespace QRKeeper.Core.Interfaces;

public interface IImageStorageService
{
    string ImagesDirectory { get; }

    Task<string> SavePngAsync(byte[] pngBytes, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string imageFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default);

    string GetImagePath(string imageFileName);
}
