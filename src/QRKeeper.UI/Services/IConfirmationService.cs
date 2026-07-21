namespace QRKeeper.UI.Services;

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);
}
