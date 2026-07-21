using QRKeeper.Core.Interfaces;

namespace QRKeeper.Infrastructure.Services;

public sealed class ImageStorageService : IImageStorageService
{
    public ImageStorageService(string appDataDirectory)
    {
        ImagesDirectory = Path.Combine(appDataDirectory, "images");
        Directory.CreateDirectory(ImagesDirectory);
    }

    public string ImagesDirectory { get; }

    public async Task<string> SavePngAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        string fileName = $"{Guid.NewGuid():N}.png";
        string path = GetImagePath(fileName);
        await File.WriteAllBytesAsync(path, pngBytes, cancellationToken);
        return fileName;
    }

    public async Task<byte[]?> ReadAsync(string imageFileName, CancellationToken cancellationToken = default)
    {
        string path = GetImagePath(imageFileName);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default)
    {
        string path = GetImagePath(imageFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetImagePath(string imageFileName)
    {
        string fileName = Path.GetFileName(imageFileName);
        return Path.Combine(ImagesDirectory, fileName);
    }
}
