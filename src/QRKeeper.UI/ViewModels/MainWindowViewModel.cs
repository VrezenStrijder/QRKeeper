using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    [ObservableProperty]
    private NavigationPageKey _selectedPage = NavigationPageKey.Home;

    public MainWindowViewModel(
        HomeViewModel home,
        SyncViewModel sync,
        BackupViewModel backup,
        SettingsViewModel settings,
        AboutViewModel about,
        IMessageService messageService)
    {
        _messageService = messageService;
        Home = home;
        Sync = sync;
        Backup = backup;
        Settings = settings;
        About = about;
        CurrentPage = Home;
        _messageService.MessageRequested += OnMessageRequested;
    }

    public HomeViewModel Home { get; }

    public SyncViewModel Sync { get; }

    public BackupViewModel Backup { get; }

    public SettingsViewModel Settings { get; }

    public AboutViewModel About { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public ObservableCollection<ToastMessageViewModel> TopRightMessages { get; } = new();

    public ObservableCollection<ToastMessageViewModel> TopCenterMessages { get; } = new();

    public ObservableCollection<ToastMessageViewModel> BottomRightMessages { get; } = new();

    public ObservableCollection<ToastMessageViewModel> BottomCenterMessages { get; } = new();

    partial void OnSelectedPageChanged(NavigationPageKey value)
    {
        CurrentPage = value switch
        {
            NavigationPageKey.Sync => Sync,
            NavigationPageKey.Backup => Backup,
            NavigationPageKey.Settings => Settings,
            NavigationPageKey.About => About,
            _ => Home
        };
    }

    public async Task LoadAsync()
    {
        await Home.LoadAsync();
        Sync.RefreshPeers();
    }

    private async void OnMessageRequested(object? sender, AppMessage message)
    {
        ToastMessageViewModel viewModel = new(message);
        ObservableCollection<ToastMessageViewModel> target = GetMessageCollection(message.Position);
        target.Add(viewModel);
        await Task.Delay(message.Duration);
        target.Remove(viewModel);
    }

    private ObservableCollection<ToastMessageViewModel> GetMessageCollection(MessagePosition position)
    {
        return position switch
        {
            MessagePosition.TopCenter => TopCenterMessages,
            MessagePosition.BottomRight => BottomRightMessages,
            MessagePosition.BottomCenter => BottomCenterMessages,
            _ => TopRightMessages
        };
    }
}
