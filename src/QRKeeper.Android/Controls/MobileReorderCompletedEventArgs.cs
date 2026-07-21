namespace QRKeeper.Android.Controls;

/// <summary>
/// Provides data for a completed mobile reorder operation.
/// </summary>
public sealed class MobileReorderCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MobileReorderCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="item">The item being moved.</param>
    /// <param name="oldIndex">The item's index before the reorder operation.</param>
    /// <param name="targetIndex">The insertion index used by the drag preview.</param>
    /// <param name="newIndex">The item's calculated index after the reorder operation.</param>
    public MobileReorderCompletedEventArgs(object item, int oldIndex, int targetIndex, int newIndex)
    {
        Item = item;
        OldIndex = oldIndex;
        TargetIndex = targetIndex;
        NewIndex = newIndex;
    }

    /// <summary>
    /// Gets the item being moved.
    /// </summary>
    public object Item { get; }

    /// <summary>
    /// Gets the item's index before the reorder operation.
    /// </summary>
    public int OldIndex { get; }

    /// <summary>
    /// Gets the insertion index used by the drag preview.
    /// </summary>
    public int TargetIndex { get; }

    /// <summary>
    /// Gets the item's calculated index after the reorder operation.
    /// </summary>
    public int NewIndex { get; }
}
