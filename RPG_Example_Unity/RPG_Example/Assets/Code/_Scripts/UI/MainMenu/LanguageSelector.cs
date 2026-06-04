using UnityEngine;

public class LanguageSelector : MonoBehaviour
{
    public OptionSelector selector;

    private GameLanguage[]  _languages;
    private SettingsService _settings;

    private void Start()
    {
        _settings  = GameServices.Get<SettingsService>();
        _languages = (GameLanguage[])System.Enum.GetValues(typeof(GameLanguage));

        string[] options = new string[_languages.Length];
        for (int i = 0; i < _languages.Length; i++)
            options[i] = GetDisplayName(_languages[i]);

        int currentIndex = System.Array.IndexOf(_languages, _settings.Language);
        selector.SetOptions(options, currentIndex);

        selector.OnValueChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(int index)
    {
        _settings.Language = _languages[index];
    }

    private string GetDisplayName(GameLanguage lang)
    {
        return lang switch
        {
            GameLanguage.English  => "English",
            GameLanguage.Spanish  => "Español",
            GameLanguage.Catalan  => "Català",
            _                     => lang.ToString()
        };
    }
}