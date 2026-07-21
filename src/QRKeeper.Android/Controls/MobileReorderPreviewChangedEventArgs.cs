namespace QRKeeper.Android.Controls;

/// <summary>
/// Provides data for a mobile reorder preview change.
/// </summary>
public sealed class MobileReorderPreviewChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MobileReorderPreviewChangedEventArgs"/> class.
    /// </summary>
    /// <param name="item">The item being moved, or null when the preview should be cleared.</param>
    /// <param name="targetIndex">The current insertion target index, or -1 when the preview should be cleared.</param>
    public MobileReorderPreviewChangedEventArgs(object? item, int targetIndex)
    {
        Item = item;
        TargetIndex = targetIndex;
    }

    /// <summary>
    /// Gets the item being moved, or null when the preview should be cleared.
    /// </summary>
    public object? Item { get; }

    /// <summary>
    /// Gets the current insertion target index, or -1 when the preview should be cleared.
    /// </summary>
    public int TargetIndex { get; }
}
