using UnityEngine;
using UnityEngine.UI;

public class VSyncSetting : MonoBehaviour
{
    public Toggle VSyncToggle;

    private SettingsService _settings;

    void Start()
    {
        _settings = GameServices.Get<SettingsService>();

        VSyncToggle.isOn = _settings.VSync;
        VSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
    }

    void OnDestroy()
    {
        VSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
    }

    void OnVSyncChanged(bool isOn)
    {
        _settings.VSync = isOn;
    }
}