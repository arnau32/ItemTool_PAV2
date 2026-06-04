using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public Slider     slider;
    public VolumeType volumeType;

    private SettingsService _settings;

    private void Start()
    {
        _settings = GameServices.Get<SettingsService>();

        slider.value = _settings.GetVolume(volumeType);
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        _settings.SetVolume(volumeType, value);
    }
}