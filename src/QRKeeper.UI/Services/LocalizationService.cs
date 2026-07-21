using System.Collections;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;

namespace QRKeeper.UI.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");
    private static readonly ResourceManager ResourceManager = new(
        "QRKeeper.UI.Resources.Strings",
        typeof(LocalizationService).Assembly);

    public LocalizationService(DesktopSettingsService settingsService)
    {
        CurrentLanguage = settingsService.Language;
    }

    public event EventHandler? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; }

    public void Apply(AppLanguage language)
    {
        CurrentLanguage = language;
        ApplyResources();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        return ResourceManager.GetString(key, GetCulture(CurrentLanguage)) ?? key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(GetCulture(CurrentLanguage), GetString(key), args);
    }

    public void ApplyResources()
    {
        IResourceDictionary resources = Application.Current?.Resources
            ?? throw new InvalidOperationException("Avalonia application resources are not available.");

        ResourceSet? resourceSet = ResourceManager.GetResourceSet(GetCulture(CurrentLanguage), true, true);
        if (resourceSet is null)
        {
            return;
        }

        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                resources[key] = value;
            }
        }
    }

    private static CultureInfo GetCulture(AppLanguage language)
    {
        return language == AppLanguage.Chinese ? ChineseCulture : EnglishCulture;
    }
}
