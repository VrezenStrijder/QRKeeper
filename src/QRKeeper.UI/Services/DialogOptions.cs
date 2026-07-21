namespace QRKeeper.UI.Services;

public sealed record DialogOptions(
    string Title,
    string Message,
    DialogKind Kind,
    IReadOnlyList<DialogButtonDefinition> Buttons);
