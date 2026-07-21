namespace QRKeeper.UI.Services;

public sealed record DialogButtonDefinition(
    string Text,
    bool Result,
    DialogButtonKind Kind,
    bool IsDefault = false,
    bool IsCancel = false);
