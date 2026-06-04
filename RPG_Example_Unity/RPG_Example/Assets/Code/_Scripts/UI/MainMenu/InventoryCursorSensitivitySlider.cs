using UnityEngine;
using UnityEngine.UI;

public class InventoryCursorSensitivitySlider : MonoBehaviour
{
    public Slider slider;

    private SettingsService _settings;

    private void Start()
    {
        _settings = GameServices.Get<SettingsService>();

        slider.value = _settings.CursorSensitivity;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        _settings.CursorSensitivity = value;
    }
}