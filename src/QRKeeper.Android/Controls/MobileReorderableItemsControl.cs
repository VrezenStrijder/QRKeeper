using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace QRKeeper.Android.Controls;

/// <summary>
/// Displays mobile record cards and centralizes touch-friendly drag reorder interaction.
/// </summary>
public class MobileReorderableItemsControl : ItemsControl
{
    /// <summary>
    /// Defines the <see cref="CanReorder"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> CanReorderProperty =
        AvaloniaProperty.Register<MobileReorderableItemsControl, bool>(nameof(CanReorder), true);

    /// <summary>
    /// Defines the <see cref="DragHandleClass"/> property.
    /// </summary>
    public static readonly StyledProperty<string> DragHandleClassProperty =
        AvaloniaProperty.Register<MobileReorderableItemsControl, string>(nameof(DragHandleClass), "record-drag-handle");

    /// <summary>
    /// Defines the <see cref="ItemCardClass"/> property.
    /// </summary>
    public static readonly StyledProperty<string> ItemCardClassProperty =
        AvaloniaProperty.Register<MobileReorderableItemsControl, string>(nameof(ItemCardClass), "record-card");

    /// <summary>
    /// Defines the <see cref="ReorderDragThreshold"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ReorderDragThresholdProperty =
        AvaloniaProperty.Register<MobileReorderableItemsControl, double>(nameof(ReorderDragThreshold), 18);

    /// <summary>
    /// Defines the <see cref="FallbackDeltaLimit"/> property.
    /// </summary>
    public static readonly StyledProperty<double> FallbackDeltaLimitProperty =
        AvaloniaProperty.Register<MobileReorderableItemsControl, double>(nameof(FallbackDeltaLimit), 24);

    private object? reorderSource; // Item currently being dragged.
    private bool reorderDetected; // Whether drag movement crossed the reorder threshold.
    private int reorderTargetIndex = -1; // Current insertion target.
    private double reorderStartY; // Pointer Y at drag start.
    private double reorderCardStartY; // Visible card center at drag start.
    private double reorderLastVectorY; // Last Thumb vector value used by fallback movement.
    private double reorderFallbackOffsetY; // Fallback offset accumulated from Thumb vectors.
    private bool reorderPointerMoved; // Whether real pointer movement was observed.
    private double? reorderPendingStartY; // Pointer Y captured before Thumb.DragStarted.

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    /// <summary>
    /// Initializes a new instance of the <see cref="MobileReorderableItemsControl"/> class.
    /// </summary>
    public MobileReorderableItemsControl()
    {
        AddHandler(PointerPressedEvent, MobileReorderableItemsControl_OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerMovedEvent, MobileReorderableItemsControl_OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(Thumb.DragStartedEvent, MobileReorderableItemsControl_OnDragStarted, RoutingStrategies.Bubble, true);
        AddHandler(Thumb.DragDeltaEvent, MobileReorderableItemsControl_OnDragDelta, RoutingStrategies.Bubble, true);
        AddHandler(Thumb.DragCompletedEvent, MobileReorderableItemsControl_OnDragCompleted, RoutingStrategies.Bubble, true);
    }

    /// <summary>
    /// Occurs when the reorder preview target changes.
    /// </summary>
    public event EventHandler<MobileReorderPreviewChangedEventArgs>? ReorderPreviewChanged;

    /// <summary>
    /// Occurs when a drag reorder operation has completed and the host should move and persist the item.
    /// </summary>
    public event EventHandler<MobileReorderCompletedEventArgs>? ReorderCompleted;

    /// <summary>
    /// Gets or sets a value indicating whether drag sorting is enabled.
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
    /// Gets or sets the minimum vertical movement required to enter reorder mode.
    /// </summary>
    public double ReorderDragThreshold
    {
        get => GetValue(ReorderDragThresholdProperty);
        set => SetValue(ReorderDragThresholdProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum Thumb vector delta used by the fallback movement path.
    /// </summary>
    public double FallbackDeltaLimit
    {
        get => GetValue(FallbackDeltaLimitProperty);
        set => SetValue(FallbackDeltaLimitProperty, value);
    }

    #region Event handling

    private void MobileReorderableItemsControl_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanReorder ||
            reorderSource is not null ||
            !IsFromDragHandle(e.Source))
        {
            return;
        }

        reorderPendingStartY = e.GetPosition(this).Y;
    }

    private void MobileReorderableItemsControl_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!CanReorder || reorderSource is null)
        {
            return;
        }

        Point current = e.GetPosition(this);
        reorderPointerMoved = true;
        UpdateReorder(current.Y - reorderStartY);
        e.Handled = true;
    }

    private void MobileReorderableItemsControl_OnDragStarted(object? sender, VectorEventArgs e)
    {
        if (!CanReorder ||
            GetDragThumb(e.Source) is not { } thumb ||
            GetItemCard(thumb) is not { Item: { } item })
        {
            return;
        }

        double startY = reorderPendingStartY ?? GetControlCenterY(thumb);
        StartReorder(item, startY);
        RaisePreview(-1);
        e.Handled = true;
    }

    private void MobileReorderableItemsControl_OnDragDelta(object? sender, VectorEventArgs e)
    {
        if (!CanReorder || reorderSource is null)
        {
            return;
        }

        if (!reorderPointerMoved)
        {
            double deltaY = GetDragVectorDelta(e.Vector.Y);
            reorderFallbackOffsetY += Math.Clamp(deltaY, -FallbackDeltaLimit, FallbackDeltaLimit);
            UpdateReorder(reorderFallbackOffsetY);
        }

        e.Handled = true;
    }

    private void MobileReorderableItemsControl_OnDragCompleted(object? sender, VectorEventArgs e)
    {
        object? source = reorderSource;
        bool shouldRaise = reorderDetected;
        int targetIndex = reorderTargetIndex;
        int oldIndex = GetItemIndex(source);
        int newIndex = GetMoveTargetIndex(source, targetIndex);

        ClearReorderState();
        e.Handled = true;

        if (shouldRaise &&
            source is not null &&
            oldIndex >= 0 &&
            newIndex >= 0 &&
            oldIndex != newIndex)
        {
            ReorderCompleted?.Invoke(this, new MobileReorderCompletedEventArgs(source, oldIndex, targetIndex, newIndex));
        }
    }

    #endregion

    #region Reorder state

    private void StartReorder(object item, double startY)
    {
        reorderSource = item;
        reorderDetected = false;
        reorderTargetIndex = -1;
        reorderStartY = startY;
        reorderCardStartY = GetItemCenterY(item) ?? startY;
        reorderLastVectorY = 0;
        reorderFallbackOffsetY = 0;
        reorderPointerMoved = false;
        reorderPendingStartY = null;
    }

    private void UpdateReorder(double rawOffsetY)
    {
        reorderDetected = Math.Abs(rawOffsetY) >= ReorderDragThreshold;
        if (!reorderDetected)
        {
            RaisePreview(-1);
            return;
        }

        double thumbY = reorderCardStartY + rawOffsetY;
        RaisePreview(GetInsertIndexAtPosition(thumbY));
    }

    private void RaisePreview(int targetIndex)
    {
        reorderTargetIndex = targetIndex;
        ReorderPreviewChanged?.Invoke(this, new MobileReorderPreviewChangedEventArgs(reorderSource, targetIndex));
    }

    private void ClearReorderState()
    {
        object? source = reorderSource;
        reorderSource = null;
        reorderDetected = false;
        reorderTargetIndex = -1;
        reorderStartY = 0;
        reorderCardStartY = 0;
        reorderLastVectorY = 0;
        reorderFallbackOffsetY = 0;
        reorderPointerMoved = false;
        reorderPendingStartY = null;

        if (source is not null)
        {
            ReorderPreviewChanged?.Invoke(this, new MobileReorderPreviewChangedEventArgs(null, -1));
        }
    }

    #endregion

    #region Lookup and geometry

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

    private static Thumb? GetDragThumb(object? source)
    {
        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Thumb thumb)
            {
                return thumb;
            }
        }

        return null;
    }

    private ItemCard? GetItemCard(object? source)
    {
        string itemCardClass = ItemCardClass;
        if (string.IsNullOrWhiteSpace(itemCardClass))
        {
            return null;
        }

        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Control control &&
                control.Classes.Contains(itemCardClass) &&
                control.DataContext is { } item)
            {
                return new ItemCard(control, item);
            }
        }

        return null;
    }

    private int GetInsertIndexAtPosition(double y)
    {
        List<Control> cards = this.GetVisualDescendants()
            .OfType<Control>()
            .Where(card => card.Classes.Contains(ItemCardClass) && card.DataContext is not null)
            .Select(card => new
            {
                Card = card,
                TopLeft = card.TranslatePoint(new Point(0, 0), this)
            })
            .Where(card => card.TopLeft.HasValue)
            .OrderBy(card => card.TopLeft!.Value.Y)
            .Select(card => card.Card)
            .ToList();

        for (int index = 0; index < cards.Count; index++)
        {
            Control card = cards[index];
            Point? topLeft = card.TranslatePoint(new Point(0, 0), this);
            double center = topLeft!.Value.Y + card.Bounds.Height / 2;
            if (y < center)
            {
                return index;
            }
        }

        return cards.Count;
    }

    private double GetDragVectorDelta(double vectorY)
    {
        double deltaY = vectorY;
        if (Math.Sign(vectorY) == Math.Sign(reorderLastVectorY) &&
            Math.Abs(vectorY) >= Math.Abs(reorderLastVectorY))
        {
            deltaY = vectorY - reorderLastVectorY;
        }

        reorderLastVectorY = vectorY;
        return deltaY;
    }

    private double GetControlCenterY(Control control)
    {
        Point? topLeft = control.TranslatePoint(new Point(0, 0), this);
        return topLeft?.Y + control.Bounds.Height / 2 ?? 0;
    }

    private double? GetItemCenterY(object item)
    {
        Control? card = this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(candidate =>
                candidate.Classes.Contains(ItemCardClass) &&
                Equals(candidate.DataContext, item));
        if (card is null)
        {
            return null;
        }

        Point? topLeft = card.TranslatePoint(new Point(0, 0), this);
        return topLeft?.Y + card.Bounds.Height / 2;
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

    private sealed record ItemCard(Control Card, object Item);

    #endregion
}
