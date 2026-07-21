using System.Globalization;
using System.Text.Json;
using QRKeeper.UI.ViewModels;

namespace QRKeeper.UI.Services;

public sealed class DesktopSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private DesktopSettingsState _state;

    public DesktopSettingsService(string appDataDirectory)
    {
        _settingsPath = Path.Combine(appDataDirectory, "desktop-settings.json");
        _state = Load();
    }

    public string DeviceId => EnsureDeviceId();

    public AppLanguage Language => _state.Language;

    public ThemeMode Theme => _state.Theme;

    public AppColorStyle ColorStyle => _state.ColorStyle;

    public bool AutoAcceptLanSyncRequests => _state.AutoAcceptLanSyncRequests;

    public void SetLanguage(AppLanguage language)
    {
        _state.Language = language;
        Save();
    }

    public void SetTheme(ThemeMode theme)
    {
        _state.Theme = theme;
        Save();
    }

    public void SetColorStyle(AppColorStyle colorStyle)
    {
        _state.ColorStyle = colorStyle;
        Save();
    }

    public void SetAutoAcceptLanSyncRequests(bool autoAccept)
    {
        _state.AutoAcceptLanSyncRequests = autoAccept;
        Save();
    }

    private DesktopSettingsState Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                DesktopSettingsState? state = JsonSerializer.Deserialize<DesktopSettingsState>(json, JsonOptions);
                if (state is not null)
                {
                    return state;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }

        return new DesktopSettingsState
        {
            Language = DetectDefaultLanguage()
        };
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_state, JsonOptions));
    }

    private string EnsureDeviceId()
    {
        if (string.IsNullOrWhiteSpace(_state.DeviceId))
        {
            _state.DeviceId = Guid.NewGuid().ToString("N");
            Save();
        }

        return _state.DeviceId;
    }

    private static AppLanguage DetectDefaultLanguage()
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Chinese
                : AppLanguage.English;
    }

    private sealed class DesktopSettingsState
    {
        public string DeviceId { get; set; } = string.Empty;

        public AppLanguage Language { get; set; } = AppLanguage.English;

        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public AppColorStyle ColorStyle { get; set; } = AppColorStyle.Ocean;

        public bool AutoAcceptLanSyncRequests { get; set; }
    }
}
