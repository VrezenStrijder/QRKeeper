using Avalonia.Controls;
using QRKeeper.UI.Views;

namespace QRKeeper.UI.Services;

public sealed class DialogConfirmationService : IConfirmationService
{
    private readonly Func<Window> _getOwner;

    public DialogConfirmationService(Func<Window> getOwner)
    {
        _getOwner = getOwner;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        FluentDialogWindow dialog = new();
        dialog.Configure(new DialogOptions(
            title,
            message,
            DialogKind.Confirm,
            [
                new DialogButtonDefinition(cancelText, false, DialogButtonKind.Secondary, IsCancel: true),
                new DialogButtonDefinition(confirmText, true, DialogButtonKind.Danger, IsDefault: true)
            ]));

        bool? result = await dialog.ShowDialog<bool?>(_getOwner());
        return result == true;
    }
}
