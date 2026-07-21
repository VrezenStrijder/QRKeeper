namespace QRKeeper.Android.Services;

public sealed class AndroidDialogService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        MainActivity? activity = MainActivity.Current;
        if (activity is null)
        {
            return Task.FromResult(false);
        }

        TaskCompletionSource<bool> completion = new();
        activity.RunOnUiThread(() =>
        {
            using global::Android.App.AlertDialog.Builder builder = new(activity);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetPositiveButton(confirmText, (_, _) => completion.TrySetResult(true));
            builder.SetNegativeButton(cancelText, (_, _) => completion.TrySetResult(false));
            builder.SetOnCancelListener(new DialogCancelListener(() => completion.TrySetResult(false)));
            builder.Show();
        });

        return completion.Task;
    }

    private sealed class DialogCancelListener : Java.Lang.Object, global::Android.Content.IDialogInterfaceOnCancelListener
    {
        private readonly Action _onCancel;

        public DialogCancelListener(Action onCancel)
        {
            _onCancel = onCancel;
        }

        public void OnCancel(global::Android.Content.IDialogInterface? dialog)
        {
            _onCancel();
        }
    }
}
