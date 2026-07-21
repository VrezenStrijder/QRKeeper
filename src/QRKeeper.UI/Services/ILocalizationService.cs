namespace QRKeeper.UI.Services;

public interface ILocalizationService
{
    event EventHandler? LanguageChanged;

    AppLanguage CurrentLanguage { get; }

    void Apply(AppLanguage language);

    string GetString(string key);

    string Format(string key, params object[] args);
}
