using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class SettingsManager : MonoBehaviour
{
    #region Fields

    private UIDocument      _document;
    private VisualElement   _settingsPanel;

    private InputService    _input;
    private SettingsService _settings;

    private PlayerInputActions InputActions => _input.Actions;

    private Slider _masterVolume;
    private Slider _bgmVolume;
    private Slider _ambienceVolume;
    private Slider _sfxVolume;

    private float _sliderInput;
    private float _step       = 2f;
    private float _repeatRate = 0.02f;
    private float _nextTime;

    private Action<InputAction.CallbackContext> _onDesactiveSettings;

    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _input    = GameServices.Get<InputService>();
        _settings = GameServices.Get<SettingsService>();

        _document      = GetComponent<UIDocument>();
        _settingsPanel = _document.rootVisualElement.Q<VisualElement>("SettingsPanel");

        _masterVolume   = _settingsPanel.Q<Slider>("MasterVolume");
        _bgmVolume      = _settingsPanel.Q<Slider>("BGMVolume");
        _ambienceVolume = _settingsPanel.Q<Slider>("AmbienceVolume");
        _sfxVolume      = _settingsPanel.Q<Slider>("SFXVolume");
    }

    private void Start()
    {
        GameServices.TryGet(out _audio);

        SetupSliderFocus(_masterVolume);
        SetupSliderFocus(_bgmVolume);
        SetupSliderFocus(_ambienceVolume);
        SetupSliderFocus(_sfxVolume);

        _masterVolume.value   = _settings.MasterVolume   * 100f;
        _bgmVolume.value      = _settings.MusicVolume    * 100f;
        _ambienceVolume.value = _settings.AmbienceVolume * 100f;
        _sfxVolume.value      = _settings.SfxVolume      * 100f;

        _masterVolume.RegisterValueChangedCallback(evt =>
        {
            _settings.MasterVolume = evt.newValue / 100f;
        });

        _bgmVolume.RegisterValueChangedCallback(evt =>
        {
            _settings.MusicVolume = evt.newValue / 100f;
        });

        _ambienceVolume.RegisterValueChangedCallback(evt =>
        {
            _settings.AmbienceVolume = evt.newValue / 100f;
        });

        _sfxVolume.RegisterValueChangedCallback(evt =>
        {
            _settings.SfxVolume = evt.newValue / 100f;
        });

        _onDesactiveSettings = MainMenu.Instance.DesActiveSettings;
        InputActions.UI_Settings.DesactiveSettings.performed += _onDesactiveSettings;

        InputActions.UI_Settings.Navigate.performed += ctx =>
        {
            _sliderInput = ctx.ReadValue<Vector2>().x;
        };

        InputActions.UI_Settings.Navigate.canceled += ctx =>
        {
            _sliderInput = 0;
        };
    }

    private void Update()
    {
        if (Mathf.Abs(_sliderInput) < 0.3f) return;
        if (Time.unscaledTime < _nextTime) return;

        var slider = _document.rootVisualElement.focusController.focusedElement as Slider;
        if (slider == null) return;

        float delta = _sliderInput > 0 ? _step : -_step;
        slider.value = Mathf.Clamp(slider.value + delta, slider.lowValue, slider.highValue);
        _nextTime = Time.unscaledTime + _repeatRate;

        PlaySound(_uiSounds?.SliderMove ?? default);
    }

    #endregion

    private void OnDestroy()
    {
        if (_input != null && _onDesactiveSettings != null)
            InputActions.UI_Settings.DesactiveSettings.performed -= _onDesactiveSettings;
    }

    #region Private

    private void SetupSliderFocus(Slider slider)
    {
        var focusFrame = slider.Q<VisualElement>("Focus_Frame");
        if (focusFrame == null) return;

        focusFrame.style.display = DisplayStyle.None;

        slider.RegisterCallback<FocusInEvent>(evt =>
        {
            focusFrame.style.display = DisplayStyle.Flex;
            PlaySound(_uiSounds?.MenuNavigate ?? default);
        });

        slider.RegisterCallback<FocusOutEvent>(evt =>
        {
            focusFrame.style.display = DisplayStyle.None;
        });
    }

    private void PlaySound(FMODUnity.EventReference sound)
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || sound.IsNull) return;
        _audio.PlayOneShot(sound, Vector3.zero);
    }

    #endregion
}
