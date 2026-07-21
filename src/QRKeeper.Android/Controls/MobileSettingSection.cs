using Avalonia;
using Avalonia.Controls;

namespace QRKeeper.Android.Controls;

/// <summary>
/// Provides a reusable mobile settings section with a title, optional value, and card content.
/// </summary>
public class MobileSettingSection : ContentControl
{
    /// <summary>
    /// Defines the <see cref="Title"/> property.
    /// </summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MobileSettingSection, string>(nameof(Title), string.Empty);

    /// <summary>
    /// Defines the <see cref="Value"/> property.
    /// </summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<MobileSettingSection, string>(nameof(Value), string.Empty);

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(MobileSettingSection);

    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the current section value shown beside the title.
    /// </summary>
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
