namespace QRKeeper.Core.Models;

public sealed record QRCodeDecodeResult(
    string Text,
    float X,
    float Y,
    float Width,
    float Height);
