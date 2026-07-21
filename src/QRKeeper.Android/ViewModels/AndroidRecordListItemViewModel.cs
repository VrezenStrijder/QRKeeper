using CommunityToolkit.Mvvm.ComponentModel;
using QRKeeper.Core.Models;

namespace QRKeeper.Android.ViewModels;

public sealed partial class AndroidRecordListItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isReordering;

    [ObservableProperty]
    private bool _isReorderBefore;

    [ObservableProperty]
    private bool _isReorderAfter;

    public AndroidRecordListItemViewModel(QRRecord record)
    {
        Record = record;
    }

    public QRRecord Record { get; }
}
