using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace QRKeeper.UI.Controls;

/// <summary>
/// Displays selectable items and raises a reorder event after desktop pointer drag sorting completes.
/// </summary>
public class ReorderableList : ListBox
{
    /// <summary>
    /// Defines the <see cref="CanReorder"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> CanReorderProperty =
        AvaloniaProperty.Register<ReorderableList, bool>(nameof(CanReorder), true);

    /// <summary>
    /// Defines the <see cref="DragHandleClass"/> property.
    /// </summary>
    public static readonly StyledProperty<string> DragHandleClassProperty =
        AvaloniaProperty.Register<ReorderableList, string>(nameof(DragHandleClass), "reorder-drag-handle");

    /// <summary>
    /// Defines the <see cref="ItemCardClass"/> property.
    /// </summary>
    public static readonly StyledProperty<string> ItemCardClassProperty =
        AvaloniaProperty.Register<ReorderableList, string>(nameof(ItemCardClass), "reorderable-list-card");

    /// <summary>
    /// Defines the <see cref="ReorderDragThreshold"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ReorderDragThresholdProperty =
        AvaloniaProperty.Register<ReorderableList, double>(nameof(ReorderDragThreshold), 8);

    private object? dragSource; // Item currently tracked as a drag candidate.
    private Point? dragStart; // Pointer position where the candidate drag started.
    private bool dragStarted; // Whether movement passed the reorder threshold.
    private int dragTargetIndex = -1; // Current insertion index preview.
    private bool dragCompleting; // Prevents intentional capture release from completing twice.

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReorderableList"/> class.
    /// </summary>
    public ReorderableList()
    {
        AddHandler(PointerPressedEvent, ReorderableList_OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerMovedEvent, ReorderableList_OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerReleasedEvent, ReorderableList_OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerCaptureLostEvent, ReorderableList_OnPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    /// <summary>
    /// Occurs when a drag reorder operation has completed and the host should move and persist the item.
    /// </summary>
    public event EventHandler<ReorderCompletedEventArgs>? ReorderCompleted;

    /// <summary>
    /// Gets or sets a value indicating whether pointer drag sorting is enabled.
    /// </summary>
    public bool CanReorder
    {
        get => GetValue(CanReorderProperty);
        set => SetValue(CanReorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the CSS class used to identify an explicit drag handle inside the item template.
    /// </summary>
    public string DragHandleClass
    {
        get => GetValue(DragHandleClassProperty);
        set => SetValue(DragHandleClassProperty, value);
    }

    /// <summary>
    /// Gets or sets the CSS class used to identify the visible card area used for insertion hit testing.
    /// </summary>
    public string ItemCardClass
    {
        get => GetValue(ItemCardClassProperty);
        set => SetValue(ItemCardClassProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum vertical pointer movement required to enter reorder mode.
    /// </summary>
    public double ReorderDragThreshold
    {
        get => GetValue(ReorderDragThresholdProperty);
        set => SetValue(ReorderDragThresholdProperty, value);
    }

    #region Pointer handling

    private void ReorderableList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanReorder || dragSource is not null)
        {
            return;
        }

        if (GetItemFromSource(e.Source) is not { } item)
        {
            ClearDragState();
            return;
        }

        if (IsFromDragHandle(e.Source))
        {
            StartDrag(item, e, true);
            return;
        }

        StartDrag(item, e, false);
    }

    private void ReorderableList_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!CanReorder ||
            dragStart is not { } start ||
            dragSource is null)
        {
            return;
        }

        Point current = e.GetPosition(this);
        if (Math.Abs(current.Y - start.Y) < ReorderDragThreshold)
        {
            return;
        }

        dragStarted = true;
        if (e.Pointer.Captured is null)
        {
            e.Pointer.Capture(this);
        }

        e.Handled = true;
        ClearReorderVisuals();
        PreviewReorder(GetInsertIndexAtPosition(current.Y));
    }

    private void ReorderableList_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        bool wasDragStarted = dragStarted;
        CompleteDrag(e.Pointer);
        if (wasDragStarted)
        {
            e.Handled = true;
        }
    }

    private void ReorderableList_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (dragCompleting)
        {
            return;
        }

        CompleteDrag(null);
    }

    #endregion

    #region Drag state

    private void StartDrag(object item, PointerPressedEventArgs e, bool captureImmediately)
    {
        dragStart = e.GetPosition(this);
        dragSource = item;
        dragStarted = false;
        dragTargetIndex = -1;

        if (captureImmediately)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void CompleteDrag(IPointer? pointer)
    {
        object? source = dragSource;
        bool shouldRaise = dragStarted;
        int targetIndex = dragTargetIndex;
        int oldIndex = GetItemIndex(source);
        int newIndex = GetMoveTargetIndex(source, targetIndex);

        dragCompleting = true;
        try
        {
            if (pointer?.Captured == this)
            {
                pointer.Capture(null);
            }
        }
        finally
        {
            dragCompleting = false;
        }

        ClearDragState();
        if (shouldRaise &&
            source is not null &&
            oldIndex >= 0 &&
            newIndex >= 0 &&
            oldIndex != newIndex)
        {
            ReorderCompleted?.Invoke(this, new ReorderCompletedEventArgs(source, oldIndex, targetIndex, newIndex));
        }
    }

    private void ClearDragState()
    {
        dragStart = null;
        dragSource = null;
        dragStarted = false;
        dragTargetIndex = -1;
        dragCompleting = false;
        ClearReorderVisuals();
    }

    #endregion

    #region Visual state

    private void PreviewReorder(int targetIndex)
    {
        dragTargetIndex = targetIndex;
        ClearReorderVisuals();

        if (dragSource is null)
        {
            return;
        }

        ListBoxItem? sourceItem = GetListBoxItemForItem(dragSource);
        sourceItem?.Classes.Add("reordering");

        int oldIndex = GetItemIndex(dragSource);
        int previewIndex = GetMoveTargetIndex(dragSource, targetIndex);
        if (previewIndex < 0 || oldIndex == previewIndex)
        {
            return;
        }

        IReadOnlyList<object?> items = GetDataItems();
        ListBoxItem? targetItem = targetIndex >= items.Count
            ? items.Count > 0 ? GetListBoxItemForItem(items[^1]) : null
            : GetListBoxItemForItem(items[targetIndex]);

        targetItem?.Classes.Add(targetIndex >= items.Count ? "reorder-after" : "reorder-before");
    }

    private void ClearReorderVisuals()
    {
        foreach (ListBoxItem item in this.GetVisualDescendants().OfType<ListBoxItem>())
        {
            item.Classes.Remove("reordering");
            item.Classes.Remove("reorder-before");
            item.Classes.Remove("reorder-after");
        }
    }

    private ListBoxItem? GetListBoxItemForItem(object? item)
    {
        return this.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .FirstOrDefault(listItem => Equals(listItem.DataContext, item));
    }

    #endregion

    #region Item lookup

    private object? GetItemFromSource(object? source)
    {
        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is ListBoxItem { DataContext: { } item })
            {
                return item;
            }
        }

        return null;
    }

    private bool IsFromDragHandle(object? source)
    {
        string dragHandleClass = DragHandleClass;
        if (string.IsNullOrWhiteSpace(dragHandleClass))
        {
            return false;
        }

        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Control control && control.Classes.Contains(dragHandleClass))
            {
                return true;
            }
        }

        return false;
    }

    private int GetInsertIndexAtPosition(double y)
    {
        List<ReorderItemBounds> items = GetVisibleItemBounds();

        for (int index = 0; index < items.Count; index++)
        {
            double center = (items[index].Top + items[index].Bottom) / 2;
            if (y < center)
            {
                return index;
            }
        }

        return items.Count;
    }

    private List<ReorderItemBounds> GetVisibleItemBounds()
    {
        return this.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(item => item.DataContext is not null)
            .Select(GetVisibleItemBounds)
            .OfType<ReorderItemBounds>()
            .OrderBy(item => item.Top)
            .ToList();
    }

    private ReorderItemBounds? GetVisibleItemBounds(ListBoxItem item)
    {
        Control anchor = GetItemMeasurementAnchor(item);
        Point? top = anchor.TranslatePoint(new Point(0, 0), this);
        Point? bottom = anchor.TranslatePoint(new Point(0, anchor.Bounds.Height), this);
        if (!top.HasValue || !bottom.HasValue)
        {
            return null;
        }

        return new ReorderItemBounds(top.Value.Y, bottom.Value.Y);
    }

    private Control GetItemMeasurementAnchor(ListBoxItem item)
    {
        string itemCardClass = ItemCardClass;
        if (!string.IsNullOrWhiteSpace(itemCardClass))
        {
            Control? card = item.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => control.Classes.Contains(itemCardClass));
            if (card is not null)
            {
                return card;
            }
        }

        return item;
    }

    private int GetItemIndex(object? item)
    {
        IReadOnlyList<object?> items = GetDataItems();
        for (int index = 0; index < items.Count; index++)
        {
            if (Equals(items[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    private int GetMoveTargetIndex(object? item, int targetIndex)
    {
        IReadOnlyList<object?> items = GetDataItems();
        int oldIndex = GetItemIndex(item);
        if (oldIndex < 0 || targetIndex < 0 || items.Count == 0)
        {
            return -1;
        }

        int newIndex = targetIndex;
        if (oldIndex < targetIndex)
        {
            newIndex--;
        }

        return Math.Clamp(newIndex, 0, items.Count - 1);
    }

    private IReadOnlyList<object?> GetDataItems()
    {
        IEnumerable? source = ItemsSource;
        source ??= Items;
        return source?.Cast<object?>().ToList() ?? [];
    }

    private sealed record ReorderItemBounds(double Top, double Bottom);

    #endregion
}
