using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Styling;
using QRKeeper.UI.Services;

namespace QRKeeper.UI.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;
    private readonly DesktopSettingsService _settingsService;

    public SettingsViewModel(
        ILocalizationService localizationService,
        DesktopSettingsService settingsService)
    {
        _localizationService = localizationService;
        _settingsService = settingsService;
        SelectedTheme = _settingsService.Theme;
        SelectedLanguage = _settingsService.Language;
        SelectedColorStyle = _settingsService.ColorStyle;
        AutoAcceptLanSyncRequests = _settingsService.AutoAcceptLanSyncRequests;
    }

    [ObservableProperty]
    private ThemeMode _selectedTheme = ThemeMode.System;

    [ObservableProperty]
    private AppLanguage _selectedLanguage;

    [ObservableProperty]
    private AppColorStyle _selectedColorStyle = AppColorStyle.Ocean;

    [ObservableProperty]
    private bool _autoAcceptLanSyncRequests;

    public bool IsSystemTheme
    {
        get => SelectedTheme == ThemeMode.System;
        set
        {
            if (value)
            {
                UseSystemTheme();
            }
        }
    }

    public bool IsLightTheme
    {
        get => SelectedTheme == ThemeMode.Light;
        set
        {
            if (value)
            {
                UseLightTheme();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => SelectedTheme == ThemeMode.Dark;
        set
        {
            if (value)
            {
                UseDarkTheme();
            }
        }
    }

    public bool IsChineseLanguage
    {
        get => SelectedLanguage == AppLanguage.Chinese;
        set
        {
            if (value)
            {
                UseChineseLanguage();
            }
        }
    }

    public bool IsEnglishLanguage
    {
        get => SelectedLanguage == AppLanguage.English;
        set
        {
            if (value)
            {
                UseEnglishLanguage();
            }
        }
    }

    public bool IsOceanColorStyle
    {
        get => SelectedColorStyle == AppColorStyle.Ocean;
        set
        {
            if (value)
            {
                UseOceanColorStyle();
            }
        }
    }

    public bool IsForestColorStyle
    {
        get => SelectedColorStyle == AppColorStyle.Forest;
        set
        {
            if (value)
            {
                UseForestColorStyle();
            }
        }
    }

    public bool IsRoseColorStyle
    {
        get => SelectedColorStyle == AppColorStyle.Rose;
        set
        {
            if (value)
            {
                UseRoseColorStyle();
            }
        }
    }

    [RelayCommand]
    public void ApplyTheme()
    {
        if (Avalonia.Application.Current is null)
        {
            return;
        }

        Avalonia.Application.Current.RequestedThemeVariant = SelectedTheme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        _settingsService.SetTheme(SelectedTheme);
        VisualStyleService.Apply(SelectedColorStyle, GetThemeVariant());
    }

    [RelayCommand]
    public void UseSystemTheme()
    {
        SelectedTheme = ThemeMode.System;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseLightTheme()
    {
        SelectedTheme = ThemeMode.Light;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseDarkTheme()
    {
        SelectedTheme = ThemeMode.Dark;
        ApplyTheme();
    }

    [RelayCommand]
    public void UseChineseLanguage()
    {
        SelectedLanguage = AppLanguage.Chinese;
        _settingsService.SetLanguage(AppLanguage.Chinese);
        _localizationService.Apply(AppLanguage.Chinese);
    }

    [RelayCommand]
    public void UseEnglishLanguage()
    {
        SelectedLanguage = AppLanguage.English;
        _settingsService.SetLanguage(AppLanguage.English);
        _localizationService.Apply(AppLanguage.English);
    }

    [RelayCommand]
    public void UseOceanColorStyle()
    {
        ApplyColorStyle(AppColorStyle.Ocean);
    }

    [RelayCommand]
    public void UseForestColorStyle()
    {
        ApplyColorStyle(AppColorStyle.Forest);
    }

    [RelayCommand]
    public void UseRoseColorStyle()
    {
        ApplyColorStyle(AppColorStyle.Rose);
    }

    private void ApplyColorStyle(AppColorStyle colorStyle)
    {
        SelectedColorStyle = colorStyle;
        _settingsService.SetColorStyle(colorStyle);
        VisualStyleService.Apply(colorStyle, GetThemeVariant());
    }

    private ThemeVariant GetThemeVariant()
    {
        return SelectedTheme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Default
        };
    }

    partial void OnAutoAcceptLanSyncRequestsChanged(bool value)
    {
        _settingsService.SetAutoAcceptLanSyncRequests(value);
    }

    partial void OnSelectedThemeChanged(ThemeMode value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
    }

    partial void OnSelectedColorStyleChanged(AppColorStyle value)
    {
        OnPropertyChanged(nameof(IsOceanColorStyle));
        OnPropertyChanged(nameof(IsForestColorStyle));
        OnPropertyChanged(nameof(IsRoseColorStyle));
    }
}
