using Avalonia.Controls;
using Avalonia.Media;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.Views;

public partial class FluentDialogWindow : Window
{
    public FluentDialogWindow()
    {
        InitializeComponent();
    }

    public void Configure(DialogOptions options)
    {
        Title = options.Title;
        TitleText.Text = options.Title;
        MessageText.Text = options.Message;

        (string icon, IBrush accent) = GetVisual(options.Kind);
        IconText.Text = icon;
        IconText.Foreground = Brushes.White;
        IconHost.Background = accent;

        ButtonPanel.Children.Clear();
        foreach (DialogButtonDefinition definition in options.Buttons)
        {
            Button button = new()
            {
                Content = definition.Text,
                MinWidth = 88,
                Classes = { GetButtonClass(definition.Kind) },
                IsDefault = definition.IsDefault,
                IsCancel = definition.IsCancel
            };
            button.Click += (_, _) => Close(definition.Result);
            ButtonPanel.Children.Add(button);
        }
    }

    private static (string Icon, IBrush Accent) GetVisual(DialogKind kind)
    {
        return kind switch
        {
            DialogKind.Success => ("✓", Brushes.SeaGreen),
            DialogKind.Warning => ("!", Brushes.DarkOrange),
            DialogKind.Error => ("×", Brushes.Firebrick),
            DialogKind.Confirm => ("?", Brushes.DodgerBlue),
            _ => ("i", Brushes.DodgerBlue)
        };
    }

    private static string GetButtonClass(DialogButtonKind kind)
    {
        return kind switch
        {
            DialogButtonKind.Primary => "primary",
            DialogButtonKind.Confirm => "confirm",
            DialogButtonKind.Danger => "danger",
            _ => "secondary"
        };
    }
}
