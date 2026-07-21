using Android.Content;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AndroidX.Core.Content;

namespace QRKeeper.Android.Services;

public sealed class AndroidQrImageShareService
{
    private readonly Func<MainActivity?> getActivity;
    private readonly Func<TopLevel?> getTopLevel;

    public AndroidQrImageShareService(
        Func<MainActivity?> getActivity,
        Func<TopLevel?> getTopLevel)
    {
        this.getActivity = getActivity;
        this.getTopLevel = getTopLevel;
    }

    public async Task<string?> SaveAsync(
        string imagePath,
        string defaultFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("QR image file was not found.", imagePath);
        }

        TopLevel topLevel = getTopLevel()
            ?? throw new InvalidOperationException("Android save picker is not available.");
        if (!topLevel.StorageProvider.CanSave)
        {
            throw new InvalidOperationException("Android save picker is not available.");
        }

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save QR image",
            SuggestedFileName = defaultFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("PNG image")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        if (file is null)
        {
            return null;
        }

        await using FileStream source = File.OpenRead(imagePath);
        await using Stream target = await file.OpenWriteAsync();
        if (target.CanSeek)
        {
            target.SetLength(0);
        }

        await source.CopyToAsync(target, cancellationToken);
        return file.TryGetLocalPath() ?? file.Name;
    }

    public Task ShareAsync(string imagePath, string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MainActivity activity = getActivity()
            ?? throw new InvalidOperationException("Android activity is not available.");
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("QR image file was not found.", imagePath);
        }

        global::Android.Net.Uri uri = FileProvider.GetUriForFile(
            activity,
            $"{activity.PackageName}.fileprovider",
            new Java.IO.File(imagePath))
            ?? throw new InvalidOperationException("QR image URI could not be created.");

        Intent shareIntent = new(Intent.ActionSend);
        shareIntent.SetType("image/png");
        shareIntent.PutExtra(Intent.ExtraStream, uri);
        shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        Intent chooser = Intent.CreateChooser(shareIntent, title)
            ?? throw new InvalidOperationException("Android share sheet could not be opened.");
        activity.StartActivity(chooser);
        return Task.CompletedTask;
    }
}
