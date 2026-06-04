using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class QualitySelector : MonoBehaviour
{
    public OptionSelector selector;
    public QualityManager qualityManager;

    public LocalizedString Low;
    public LocalizedString Medium;
    public LocalizedString High;
    public LocalizedString Ultra;

    private SettingsService _settings;

    void Start()
    {
        _settings = GameServices.Get<SettingsService>();

        qualityManager = GameServices.Get<QualityManager>();

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        InitSelector();
    }

    void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        InitSelector();
    }

    void InitSelector()
    {
        string[] qualityOptions =
        {
            Low.GetLocalizedString(),
            Medium.GetLocalizedString(),
            High.GetLocalizedString(),
            Ultra.GetLocalizedString()
        };

        selector.SetOptions(qualityOptions, _settings.QualityLevel);

        selector.OnValueChanged -= OnQualityChanged;
        selector.OnValueChanged += OnQualityChanged;
    }

    void OnQualityChanged(int index)
    {
        qualityManager.SetQuality(index);
        _settings.QualityLevel = index;
    }
}