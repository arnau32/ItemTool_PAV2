using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class SettingsService : MonoBehaviour, IGameServices, IInitializable
{
    private const string FILE_NAME   = "settings.json";
    private const float  SAVE_DELAY  = 0.5f;

    #region Fields

    private SettingsSaveData  _data;
    private AudioService      _audio;
    private LanguageManager   _language;
    private InputIconSettings _inputIcons;

    private bool  _dirty;
    private float _nextSave;

    private string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _data = LoadFromDisk();
    }

    public void Initialize()
    {
        _audio      = GameServices.Get<AudioService>();
        _language   = GameServices.Get<LanguageManager>();
        _inputIcons = GameServices.Get<InputIconSettings>();

        ApplyAudio();
        ApplyQuality();

        if (_data.inputDeviceOverride)
            _inputIcons.SetDevice((InputDeviceType)_data.inputDevice);

        InventoryCursorSensitivity.Instance?.ApplySensitivity(_data.cursorSensitivity);
    }

    private void Start()
    {
        StartCoroutine(ApplyLanguageAfterLocalizationInit());
    }

    private void Update()
    {
        if (!_dirty) return;
        if (Time.unscaledTime < _nextSave) return;

        WriteToDisk();
        _dirty = false;
    }

    private void OnApplicationQuit()
    {
        if (_dirty) WriteToDisk();
    }

    #endregion

    #region Properties

    public float MasterVolume
    {
        get => _data.masterVolume;
        set
        {
            _data.masterVolume  = Mathf.Clamp01(value);
            _audio.MasterVolume = _data.masterVolume;
            MarkDirty();
        }
    }

    public float MusicVolume
    {
        get => _data.musicVolume;
        set
        {
            _data.musicVolume  = Mathf.Clamp01(value);
            _audio.MusicVolume = _data.musicVolume;
            MarkDirty();
        }
    }

    public float AmbienceVolume
    {
        get => _data.ambienceVolume;
        set
        {
            _data.ambienceVolume  = Mathf.Clamp01(value);
            _audio.AmbienceVolume = _data.ambienceVolume;
            MarkDirty();
        }
    }

    public float SfxVolume
    {
        get => _data.sfxVolume;
        set
        {
            _data.sfxVolume  = Mathf.Clamp01(value);
            _audio.SFXVolume = _data.sfxVolume;
            MarkDirty();
        }
    }

    public GameLanguage Language
    {
        get => (GameLanguage)_data.language;
        set
        {
            _data.language = (int)value;
            _language.SetLanguage(value);
            MarkDirty();
        }
    }

    public InputDeviceType InputDevice
    {
        get => (InputDeviceType)_data.inputDevice;
        set
        {
            _data.inputDevice         = (int)value;
            _data.inputDeviceOverride = true;
            _inputIcons.SetDevice(value);
            MarkDirty();
        }
    }

    public float CursorSensitivity
    {
        get => _data.cursorSensitivity;
        set
        {
            _data.cursorSensitivity = Mathf.Clamp01(value);

            InventoryCursorSensitivity.Instance?.ApplySensitivity(_data.cursorSensitivity);

            MarkDirty();
        }
    }

    public int QualityLevel
    {
        get => _data.qualityLevel;
        set
        {
            _data.qualityLevel = Mathf.Clamp(value, 0, 3);
            MarkDirty();
        }
    }

    #endregion

    #region Public API

    public float GetVolume(VolumeType type)
    {
        return type switch
        {
            VolumeType.Master   => _data.masterVolume,
            VolumeType.BGM      => _data.musicVolume,
            VolumeType.Ambience => _data.ambienceVolume,
            VolumeType.SFX      => _data.sfxVolume,
            _                   => 1f,
        };
    }

    public void SetVolume(VolumeType type, float value)
    {
        switch (type)
        {
            case VolumeType.Master:   MasterVolume   = value; break;
            case VolumeType.BGM:      MusicVolume    = value; break;
            case VolumeType.Ambience: AmbienceVolume = value; break;
            case VolumeType.SFX:      SfxVolume      = value; break;
        }
    }

    public bool VSync
    {
        get => _data.vsync;
        set
        {
            _data.vsync = value;
            QualitySettings.vSyncCount = value ? 1 : 0;
            MarkDirty();
        }
    }

    #endregion

    #region Private

    private IEnumerator ApplyLanguageAfterLocalizationInit()
    {
        yield return new WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone);
        _language.SetLanguage((GameLanguage)_data.language);
    }

    private void ApplyAudio()
    {
        _audio.MasterVolume   = _data.masterVolume;
        _audio.MusicVolume    = _data.musicVolume;
        _audio.AmbienceVolume = _data.ambienceVolume;
        _audio.SFXVolume      = _data.sfxVolume;
    }

    private void ApplyQuality()
    {
        if (GameServices.TryGet<QualityManager>(out var qm))
            qm.SetQuality(_data.qualityLevel);
        QualitySettings.vSyncCount = _data.vsync ? 1 : 0;
    }

    private void MarkDirty()
    {
        _dirty    = true;
        _nextSave = Time.unscaledTime + SAVE_DELAY;
    }

    private void WriteToDisk()
    {
        File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
    }

    private SettingsSaveData LoadFromDisk()
    {
        if (!File.Exists(FilePath))
            return new SettingsSaveData();

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<SettingsSaveData>(json) ?? new SettingsSaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsService] Failed to load settings: {e.Message}");
            return new SettingsSaveData();
        }
    }

    #endregion
}