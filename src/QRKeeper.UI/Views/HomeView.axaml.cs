using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using QRKeeper.Core.Models;
using QRKeeper.UI.Controls;
using QRKeeper.UI.ViewModels;

namespace QRKeeper.UI.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void HomeView_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void HomeView_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.Items
            .Select(item => item.TryGetFile())
            .OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath())
            .FirstOrDefault(localPath => !string.IsNullOrWhiteSpace(localPath));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.ImportImagePathAsync(path);
    }

    private async void RecordList_OnReorderCompleted(object? sender, ReorderCompletedEventArgs e)
    {
        if (e.Item is not QRRecord record ||
            DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        viewModel.MoveRecordToIndex(record, e.TargetIndex);
        await viewModel.PersistRecordOrderAsync(record);
    }
}
