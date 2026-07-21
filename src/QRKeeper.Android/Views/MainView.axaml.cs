using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using QRKeeper.Android.Controls;
using QRKeeper.Android.ViewModels;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.Views;

public partial class MainView : UserControl
{
    private const double SwipeDeleteThreshold = -52;
    private const double SwipeVerticalTolerance = 56;

    private Point? _recordSwipeStart;
    private QRRecord? _recordSwipeRecord;
    private Control? _recordSwipeCard;
    private bool _recordSwipeDetected;
    private DateTimeOffset _recordPointerPressedAt;
    private bool _suppressNextRecordTap;

    public MainView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, RecordCard_OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerMovedEvent, RecordCard_OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerReleasedEvent, RecordCard_OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(PointerCaptureLostEvent, RecordCard_OnPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        SizeChanged += MainView_SizeChanged;
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.LoadAsync();
                if (MainActivity.Current is { } activity)
                {
                    activity.SharedImageReceived += OnSharedImageReceived;
                    activity.TryImportPendingSharedImage(viewModel);
                }
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (MainActivity.Current is { } activity)
            {
                activity.SharedImageReceived -= OnSharedImageReceived;
            }
        };
    }

    private void MainView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        bool useWideLayout = Bounds.Width >= 720;
        RecordsLayoutRoot.Classes.Set("wide", useWideLayout);
        RecordsLayoutRoot.ColumnDefinitions = new ColumnDefinitions(useWideLayout ? "340,*" : "*,0");
        Grid.SetColumn(RecordDetailHost, useWideLayout ? 1 : 0);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.UseWideLayout = useWideLayout;
        }
    }

    private void RecordCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        (Control? card, QRRecord? record) = GetRecordCard(e.Source);
        if (IsFromButton(e.Source) ||
            card is null ||
            record is null)
        {
            _recordSwipeStart = null;
            _recordSwipeRecord = null;
            _recordSwipeCard = null;
            return;
        }

        if (IsFromDragHandle(e.Source))
        {
            ClearSwipeState();
            return;
        }

        _recordSwipeStart = e.GetPosition(this);
        _recordSwipeRecord = record;
        _recordSwipeCard = card;
        _recordSwipeDetected = false;
        _recordPointerPressedAt = DateTimeOffset.UtcNow;
    }

    private void RecordCard_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_recordSwipeStart is not { } start || _recordSwipeRecord is null)
        {
            return;
        }

        Point current = e.GetPosition(this);
        double deltaX = current.X - start.X;
        double deltaY = Math.Abs(current.Y - start.Y);
        if (deltaX <= SwipeDeleteThreshold && deltaY <= SwipeVerticalTolerance)
        {
            _recordSwipeDetected = true;
            if (_recordSwipeCard is not null && e.Pointer.Captured is null)
            {
                e.Pointer.Capture(_recordSwipeCard);
            }

            e.Handled = true;
        }
    }

    private async void RecordCard_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_recordSwipeStart is not { } start ||
            _recordSwipeRecord is not { } record)
        {
            ClearSwipeState();
            return;
        }

        Point end = e.GetPosition(this);
        double deltaX = end.X - start.X;
        double deltaY = Math.Abs(end.Y - start.Y);
        if (e.Pointer.Captured is not null)
        {
            e.Pointer.Capture(null);
        }

        bool shouldDelete = _recordSwipeDetected || (deltaX <= SwipeDeleteThreshold && deltaY <= SwipeVerticalTolerance);
        ClearSwipeState();

        if (!shouldDelete)
        {
            return;
        }

        _suppressNextRecordTap = true;
        e.Handled = true;
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.DeleteRecordAsync(record);
        }
    }

    private void RecordCard_OnTapped(object? sender, TappedEventArgs e)
    {
        if (_suppressNextRecordTap)
        {
            _suppressNextRecordTap = false;
            e.Handled = true;
            return;
        }

        if (IsFromButton(e.Source) ||
            IsFromDragHandle(e.Source) ||
            GetRecordCard(sender).Record is not { } record ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedRecord = record;
        viewModel.OpenSelectedRecord();
        e.Handled = true;
    }

    private void RecordCard_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ClearSwipeState();
    }

    private void RecordList_OnReorderPreviewChanged(object? sender, MobileReorderPreviewChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (e.Item is AndroidRecordListItemViewModel item)
        {
            viewModel.PreviewReorderTargetIndex(item.Record, e.TargetIndex);
            return;
        }

        viewModel.ClearReorderTarget();
    }

    private async void RecordList_OnReorderCompleted(object? sender, MobileReorderCompletedEventArgs e)
    {
        if (e.Item is AndroidRecordListItemViewModel item &&
            DataContext is MainViewModel viewModel)
        {
            _suppressNextRecordTap = true;
            // Let Android Thumb finish touch-capture cleanup before the ItemsControl source changes.
            await Task.Yield();
            await viewModel.MoveRecordToIndexAsync(item.Record, e.TargetIndex);
        }
    }

    private void OnSharedImageReceived(global::Android.Net.Uri uri)
    {
        if (DataContext is MainViewModel viewModel)
        {
            _ = viewModel.ImportSharedImageAsync(uri);
        }
    }

    private static bool IsFromButton(object? source)
    {
        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Avalonia.Controls.Button)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFromDragHandle(object? source)
    {
        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Control control && control.Classes.Contains("record-drag-handle"))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearSwipeState()
    {
        _recordSwipeStart = null;
        _recordSwipeRecord = null;
        _recordSwipeCard = null;
        _recordSwipeDetected = false;
    }

    private static (Control? Card, QRRecord? Record) GetRecordCard(object? source)
    {
        for (Avalonia.Visual? visual = source as Avalonia.Visual;
             visual is not null;
             visual = visual.GetVisualParent())
        {
            if (visual is Control control &&
                control.Classes.Contains("record-card") &&
                control.DataContext is AndroidRecordListItemViewModel item)
            {
                return (control, item.Record);
            }
        }

        return (null, null);
    }
}
