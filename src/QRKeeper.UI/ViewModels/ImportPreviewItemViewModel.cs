using CommunityToolkit.Mvvm.ComponentModel;
using QRKeeper.Core.Models;

namespace QRKeeper.UI.ViewModels;

public sealed partial class ImportPreviewItemViewModel : ViewModelBase
{
    public ImportPreviewItemViewModel(ImportPreviewItem item)
    {
        Item = item;
        _isSelected = item.IsSelected;
    }

    public ImportPreviewItem Item { get; }

    public QRRecord Record => Item.Record;

    public bool IsDuplicate => Item.IsDuplicate;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        Item.IsSelected = value;
    }
}
