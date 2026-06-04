using System;

public enum VolumeType
{
    Master,
    BGM,
    Ambience,
    SFX,
}

[Serializable]
public class SettingsSaveData
{
    public float masterVolume        = 1f;
    public float musicVolume         = 1f;
    public float ambienceVolume      = 1f;
    public float sfxVolume           = 1f;
    public int   language            = 1; // GameLanguage.Spanish
    public int   inputDevice         = 0; // InputDeviceType.Xbox
    public bool  inputDeviceOverride = false;

    public float cursorSensitivity = 0.5f;
    public int qualityLevel = 2; // QualityManager.QualityLevel.High
    public bool vsync = true;
}