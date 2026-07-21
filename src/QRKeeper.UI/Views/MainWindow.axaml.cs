using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using QRKeeper.UI.ViewModels;

namespace QRKeeper.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.LoadAsync();
            }
        };
    }

    private async void RootNavigation_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            e.SelectedItem is not NavigationViewItem item ||
            item.Tag is not string page)
        {
            return;
        }

        viewModel.SelectedPage = page switch
        {
            "Sync" => NavigationPageKey.Sync,
            "Backup" => NavigationPageKey.Backup,
            "Settings" => NavigationPageKey.Settings,
            "About" => NavigationPageKey.About,
            _ => NavigationPageKey.Home
        };

        if (viewModel.CurrentPage == viewModel.Home)
        {
            try
            {
                await viewModel.Home.RefreshAsync();
            }
            catch (Exception ex)
            {
                viewModel.Home.StatusText = ex.Message;
            }
        }
        else if (viewModel.CurrentPage == viewModel.Sync)
        {
            viewModel.Sync.RefreshPeers();
        }
    }
}
