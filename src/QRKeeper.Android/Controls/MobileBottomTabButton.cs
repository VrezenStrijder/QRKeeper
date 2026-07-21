using Avalonia;

namespace QRKeeper.Android.Controls;

/// <summary>
/// Represents a reusable bottom navigation tab button for the Android shell.
/// </summary>
public class MobileBottomTabButton : Avalonia.Controls.Button
{
    /// <summary>
    /// Defines the <see cref="Label"/> property.
    /// </summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MobileBottomTabButton, string>(nameof(Label), string.Empty);

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(MobileBottomTabButton);

    /// <summary>
    /// Gets or sets the tab label.
    /// </summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
