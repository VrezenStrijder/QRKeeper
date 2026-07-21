namespace QRKeeper.Core.Common;

public static class AppConstants
{
    public const string AppName = "QRKeeper";
    public const string AppVersion = "1.0.3";
    public const string UpdateRepositoryOwner = "VrezenStrijder";
    public const string UpdateRepositoryName = "QRKeeper";
    public const string UpdateManifestAssetName = "update.json";
    public const int SyncProtocolVersion = 1;
    public const string BackupExtension = ".qrbak";
    public const int MaxNameLength = 100;
    public const int MaxNoteLength = 500;
    public const long MaxImageBytes = 20 * 1024 * 1024;
    public const int QrImageSize = 300;
}
