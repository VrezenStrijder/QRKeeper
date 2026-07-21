namespace QRKeeper.UI.Services;

public interface IScreenCaptureService
{
    Task<ScreenCaptureResult> CaptureScreenAsync(CancellationToken cancellationToken = default);
}

public sealed record ScreenCaptureResult(Stream? ImageStream, string? DecodedText, string? Message)
{
    public static ScreenCaptureResult FromStream(Stream stream)
    {
        return new ScreenCaptureResult(stream, null, null);
    }

    public static ScreenCaptureResult FromDecodedText(string decodedText)
    {
        return new ScreenCaptureResult(null, decodedText, null);
    }

    public static ScreenCaptureResult Empty(string message)
    {
        return new ScreenCaptureResult(null, null, message);
    }
}
