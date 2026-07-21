using System.Globalization;
using System.Text.Json;

namespace QRKeeper.Android.Services;

public enum AndroidAppLanguage
{
    English,
    Chinese
}

public enum AndroidThemeMode
{
    System,
    Light,
    Dark
}

public enum AndroidColorStyle
{
    Ocean,
    Forest,
    Rose
}

public sealed class AndroidSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private AndroidSettingsState _state;

    public AndroidSettingsService(string appDataDirectory)
    {
        _settingsPath = Path.Combine(appDataDirectory, "android-settings.json");
        _state = Load();
    }

    public string DeviceId => EnsureDeviceId();

    public AndroidAppLanguage Language => _state.Language;

    public AndroidThemeMode Theme => _state.Theme;

    public AndroidColorStyle ColorStyle => _state.ColorStyle;

    public bool AutoAcceptLanSyncRequests => _state.AutoAcceptLanSyncRequests;

    public void SetLanguage(AndroidAppLanguage language)
    {
        _state.Language = language;
        Save();
    }

    public void SetTheme(AndroidThemeMode theme)
    {
        _state.Theme = theme;
        Save();
    }

    public void SetColorStyle(AndroidColorStyle colorStyle)
    {
        _state.ColorStyle = colorStyle;
        Save();
    }

    public void SetAutoAcceptLanSyncRequests(bool autoAccept)
    {
        _state.AutoAcceptLanSyncRequests = autoAccept;
        Save();
    }

    private AndroidSettingsState Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                AndroidSettingsState? state = JsonSerializer.Deserialize<AndroidSettingsState>(json, JsonOptions);
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

        return new AndroidSettingsState
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

    private static AndroidAppLanguage DetectDefaultLanguage()
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        return culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
                ? AndroidAppLanguage.Chinese
                : AndroidAppLanguage.English;
    }

    private sealed class AndroidSettingsState
    {
        public string DeviceId { get; set; } = string.Empty;

        public AndroidAppLanguage Language { get; set; } = AndroidAppLanguage.English;

        public AndroidThemeMode Theme { get; set; } = AndroidThemeMode.System;

        public AndroidColorStyle ColorStyle { get; set; } = AndroidColorStyle.Ocean;

        public bool AutoAcceptLanSyncRequests { get; set; }
    }
}
