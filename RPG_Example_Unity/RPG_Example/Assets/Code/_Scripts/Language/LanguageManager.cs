using UnityEngine;
using UnityEngine.Localization.Settings;

public enum GameLanguage
{
    English,
    Spanish,
    Catalan
}

public class LanguageManager : MonoBehaviour, IGameServices
{
    public GameLanguage CurrentLanguage { get; private set; }

    #region Public API

    public void SetLanguage(GameLanguage lang)
    {
        var locales = LocalizationSettings.AvailableLocales;

        LocalizationSettings.SelectedLocale = lang switch
        {
            GameLanguage.Spanish => locales.GetLocale("es"),
            GameLanguage.Catalan => locales.GetLocale("ca"),
            _                    => locales.GetLocale("en"),
        };

        CurrentLanguage = lang;
    }

    #endregion
}