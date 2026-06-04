using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public class AudioService : MonoBehaviour, IGameServices, IShutdownable
{
    #region Fields

    [Header("Global Audio Events")] [SerializeField]
    private AmbienceMusicSounds _ambienceMusicSounds;

    [Header("Volume")] [Range(0, 1)] [SerializeField]
    private float _masterVolume = 1f;

    [Range(0, 1)] [SerializeField] private float _musicVolume = 1f;
    [Range(0, 1)] [SerializeField] private float _ambienceVolume = 1f;
    [Range(0, 1)] [SerializeField] private float _sfxVolume = 1f;

    private Bus _masterBus;
    private Bus _musicBus;
    private Bus _ambienceBus;
    private Bus _sfxBus;

    [Header("Combat Music Fade")]
    [SerializeField] private float _combatFadeInDuration  = 1.5f;
    [SerializeField] private float _combatFadeOutDuration = 3f;

    // Persistent instances — music and ambience. Never released until Shutdown.
    // Tracked by name so callers can drive parameters directly.
    private EventInstance _musicEventInstance;
    private EventInstance _ambienceEventInstance;
    private EventInstance _combatMusicInstance;

    private readonly List<EventInstance> _ambientLayerInstances = new(4);
    private readonly List<EventInstance> _managedInstances      = new(16);
    private readonly List<StudioEventEmitter> _eventEmitters    = new(8);

    private bool _backgroundAudioStarted;
    private bool _inCombat;
    private bool _inMusicOverride;
    private Coroutine _combatFadeCoroutine;
    private Coroutine _overrideFadeCoroutine;
    private Coroutine _restartFadeCoroutine;
    private EventInstance _overrideMusicInstance;

    // FMOD parameter names — must match your FMOD Studio project exactly.
    private const string MusicStateParam = "MusicState";

    #endregion

    #region Properties

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Mathf.Clamp01(value);
            _masterBus.setVolume(_masterVolume);
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            _musicBus.setVolume(_musicVolume);
        }
    }

    public float AmbienceVolume
    {
        get => _ambienceVolume;
        set
        {
            _ambienceVolume = Mathf.Clamp01(value);
            _ambienceBus.setVolume(_ambienceVolume);
        }
    }

    public float SFXVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp01(value);
            _sfxBus.setVolume(_sfxVolume);
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _masterBus = RuntimeManager.GetBus("bus:/");
        _musicBus = RuntimeManager.GetBus("bus:/Music");
        _ambienceBus = RuntimeManager.GetBus("bus:/Ambience");
        _sfxBus = RuntimeManager.GetBus("bus:/SFX");

        _masterBus.setVolume(_masterVolume);
        _musicBus.setVolume(_musicVolume);
        _ambienceBus.setVolume(_ambienceVolume);
        _sfxBus.setVolume(_sfxVolume);
    }

    private void Start()
    {
        if (_ambienceMusicSounds == null)
            Debug.LogError("[AudioService] AmbienceMusicSounds asset not assigned.", this);
    }

    #endregion

    #region IShutdownable

    public void Shutdown()
    {
        StopAndRelease(ref _musicEventInstance);
        StopAndRelease(ref _ambienceEventInstance);
        StopAndRelease(ref _combatMusicInstance);
        StopAndRelease(ref _overrideMusicInstance);
        StopAmbientLayers();

        foreach (var instance in _managedInstances)
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }

        _managedInstances.Clear();

        foreach (var eventEmitter in _eventEmitters)
        {
            if (eventEmitter == null) continue;
            eventEmitter.Stop();
        }

        _eventEmitters.Clear();
    }

    #endregion

    #region Public API — Music & Ambience

    // Must be called by SceneMusicStarter before any area or parameter change.
    // Idempotent: safe to call from every scene; only initializes once.
    public void StartBackgroundAudio()
    {
        if (_backgroundAudioStarted) return;
        if (_ambienceMusicSounds == null)
        {
            Debug.LogError("[AudioService] AmbienceMusicSounds asset not assigned.", this);
            return;
        }

        InitializeMusic(_ambienceMusicSounds.Music);
        InitializeAmbience(_ambienceMusicSounds.Ambience);
        InitializeAmbientLayers(_ambienceMusicSounds.AmbientLayers);
        _backgroundAudioStarted = true;
    }

    public void SetMusicArea(Enums.MusicArea area)
    {
        if (!_backgroundAudioStarted) return;
        _musicEventInstance.setParameterByName(MusicStateParam, (float)area);
    }

    public void RestartMusicAtArea(Enums.MusicArea area)
    {
        StopAndRelease(ref _musicEventInstance);
        InitializeMusic(_ambienceMusicSounds.Music);
        _musicEventInstance.setParameterByName(MusicStateParam, (float)area);
    }

    public void RestartMusicAtAreaFaded(Enums.MusicArea area, float fadeOutDuration, float fadeInDuration)
    {
        if (_restartFadeCoroutine != null) StopCoroutine(_restartFadeCoroutine);
        _restartFadeCoroutine = StartCoroutine(RestartMusicFaded(area, fadeOutDuration, fadeInDuration));
    }

    public void SetAmbienceParameter(string parameterName, float parameterValue)
    {
        if (!_backgroundAudioStarted) return;
        _ambienceEventInstance.setParameterByName(parameterName, parameterValue);
    }

    public void SetGlobalParameter(string paramName, float value)
        => RuntimeManager.StudioSystem.setParameterByName(paramName, value);

    public void StopBackgroundAudio()
    {
        StopAndRelease(ref _musicEventInstance);
        StopAndRelease(ref _ambienceEventInstance);
        StopAndRelease(ref _combatMusicInstance);
        StopAndRelease(ref _overrideMusicInstance);
        StopAmbientLayers();
        _backgroundAudioStarted = false;
        _inCombat        = false;
        _inMusicOverride = false;
    }

    // Fades out the current music, plays overrideEvent, and remembers to restore on StopMusicOverride.
    public void PlayMusicOverride(FMODUnity.EventReference overrideEvent, float fadeDuration = 1f)
    {
        if (overrideEvent.IsNull) return;
        if (_overrideFadeCoroutine != null) StopCoroutine(_overrideFadeCoroutine);
        _overrideFadeCoroutine = StartCoroutine(FadeInOverride(overrideEvent, fadeDuration));
    }

    // Fades out the override and restores the main music.
    public void StopMusicOverride(float fadeDuration = 1f)
    {
        if (!_inMusicOverride) return;
        if (_overrideFadeCoroutine != null) StopCoroutine(_overrideFadeCoroutine);
        _overrideFadeCoroutine = StartCoroutine(FadeOutOverride(fadeDuration));
    }

    public void StopBackgroundAudioFaded(float fadeDuration = 1f)
    {
        StartCoroutine(FadeOutBackgroundAndStop(fadeDuration));
    }

    public void EnterCombatMusic()
    {
        if (_inCombat) return;
        if (_ambienceMusicSounds == null || _ambienceMusicSounds.CombatMusic.IsNull) return;
        _inCombat = true;
        if (_combatFadeCoroutine != null) StopCoroutine(_combatFadeCoroutine);
        _combatFadeCoroutine = StartCoroutine(CrossfadeToCombat());
    }

    public void ExitCombatMusic()
    {
        if (!_inCombat) return;
        _inCombat = false;
        if (_combatFadeCoroutine != null) StopCoroutine(_combatFadeCoroutine);
        _combatFadeCoroutine = StartCoroutine(CrossfadeToExploration(_combatFadeOutDuration));
    }

    // Exits combat music with a custom fade duration — used for player death.
    public void ForceExitCombatMusic(float fadeDuration)
    {
        if (!_inCombat) return;
        _inCombat = false;
        if (_combatFadeCoroutine != null) StopCoroutine(_combatFadeCoroutine);
        _combatFadeCoroutine = StartCoroutine(CrossfadeToExploration(fadeDuration));
    }

    #endregion

    #region Public API — Playback

    // Fire-and-forget one shot. Use for hits, footsteps, UI clicks.
    public void PlayOneShot(EventReference sound, Vector3 worldPos)
        => RuntimeManager.PlayOneShot(sound, worldPos);

    // Same as PlayOneShot but the sound follows the owner's position every frame.
    public void PlayOneShotAttached(EventReference sound, GameObject owner)
        => RuntimeManager.PlayOneShotAttached(sound, owner);

    // Returns a tracked instance for sounds needing manual control (looping, parameters).
    public EventInstance CreateInstance(EventReference eventReference)
    {
        var instance = RuntimeManager.CreateInstance(eventReference);
        _managedInstances.Add(instance);
        return instance;
    }

    public StudioEventEmitter InitializeEventEmitter(EventReference eventReference, GameObject emitterGameObject)
    {
        var emitter = emitterGameObject.GetComponent<StudioEventEmitter>();
        emitter.EventReference = eventReference;
        _eventEmitters.Add(emitter);
        return emitter;
    }

    #endregion

    #region Private

    private void InitializeMusic(EventReference eventReference)
    {
        _musicEventInstance = RuntimeManager.CreateInstance(eventReference);
        _musicEventInstance.setVolume(1f);
        _musicEventInstance.start();
    }

    private void InitializeAmbience(EventReference eventReference)
    {
        _ambienceEventInstance = RuntimeManager.CreateInstance(eventReference);
        _ambienceEventInstance.start();
    }

    private void InitializeAmbientLayers(EventReference[] layers)
    {
        if (layers == null) return;
        foreach (var layer in layers)
        {
            if (layer.IsNull) continue;
            var instance = RuntimeManager.CreateInstance(layer);
            instance.start();
            _ambientLayerInstances.Add(instance);
        }
    }

    private void StopAmbientLayers()
    {
        foreach (var instance in _ambientLayerInstances)
        {
            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
        }
        _ambientLayerInstances.Clear();
    }

    private IEnumerator CrossfadeToCombat()
    {
        if (_combatMusicInstance.isValid())
        {
            _combatMusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _combatMusicInstance.release();
        }

        _combatMusicInstance = RuntimeManager.CreateInstance(_ambienceMusicSounds.CombatMusic);
        _combatMusicInstance.setVolume(0f);
        _combatMusicInstance.start();

        float elapsed   = 0f;
        float startVol  = GetInstanceVolume(_musicEventInstance);

        while (elapsed < _combatFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / _combatFadeInDuration);
            _musicEventInstance.setVolume(Mathf.Lerp(startVol, 0f, t));
            _combatMusicInstance.setVolume(t);
            yield return null;
        }

        _musicEventInstance.setVolume(0f);
        _combatMusicInstance.setVolume(1f);
        _combatFadeCoroutine = null;
    }

    private IEnumerator CrossfadeToExploration(float duration)
    {
        float elapsed  = 0f;
        float startVol = GetInstanceVolume(_combatMusicInstance);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            _combatMusicInstance.setVolume(Mathf.Lerp(startVol, 0f, t));
            if (_musicEventInstance.isValid())
                _musicEventInstance.setVolume(t);
            yield return null;
        }

        _combatMusicInstance.setVolume(0f);
        _combatMusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _combatMusicInstance.release();
        _combatMusicInstance = default;
        if (_musicEventInstance.isValid())
            _musicEventInstance.setVolume(1f);
        _combatFadeCoroutine = null;
    }

    private IEnumerator RestartMusicFaded(Enums.MusicArea area, float fadeOutDuration, float fadeInDuration)
    {
        // Fade out current instance.
        float elapsed  = 0f;
        float startVol = GetInstanceVolume(_musicEventInstance);

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicEventInstance.setVolume(Mathf.Lerp(startVol, 0f, Mathf.Clamp01(elapsed / fadeOutDuration)));
            yield return null;
        }

        _musicEventInstance.setVolume(0f);
        StopAndRelease(ref _musicEventInstance);

        // Start fresh instance at silence, then fade in.
        _musicEventInstance = RuntimeManager.CreateInstance(_ambienceMusicSounds.Music);
        _musicEventInstance.setVolume(0f);
        _musicEventInstance.setParameterByName(MusicStateParam, (float)area);
        _musicEventInstance.start();

        elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicEventInstance.setVolume(Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }

        _musicEventInstance.setVolume(1f);
        _restartFadeCoroutine = null;
    }

    private static float GetInstanceVolume(EventInstance instance)
    {
        if (!instance.isValid()) return 0f;
        instance.getVolume(out float vol);
        return vol;
    }

    private IEnumerator FadeInOverride(FMODUnity.EventReference overrideEvent, float duration)
    {
        // Stop any previous override cleanly
        if (_overrideMusicInstance.isValid())
        {
            _overrideMusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _overrideMusicInstance.release();
        }

        _overrideMusicInstance = RuntimeManager.CreateInstance(overrideEvent);
        _overrideMusicInstance.setVolume(0f);
        _overrideMusicInstance.start();
        _inMusicOverride = true;

        float elapsed   = 0f;
        float startVol  = GetInstanceVolume(_musicEventInstance);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            _musicEventInstance.setVolume(Mathf.Lerp(startVol, 0f, t));
            _overrideMusicInstance.setVolume(t);
            yield return null;
        }

        _musicEventInstance.setVolume(0f);
        _overrideMusicInstance.setVolume(1f);
        _overrideFadeCoroutine = null;
    }

    private IEnumerator FadeOutOverride(float duration)
    {
        float elapsed   = 0f;
        float startVol  = GetInstanceVolume(_overrideMusicInstance);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            _overrideMusicInstance.setVolume(Mathf.Lerp(startVol, 0f, t));
            _musicEventInstance.setVolume(t);
            yield return null;
        }

        _overrideMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _overrideMusicInstance.release();
        _overrideMusicInstance = default;
        _musicEventInstance.setVolume(1f);
        _inMusicOverride         = false;
        _overrideFadeCoroutine   = null;
    }

    private IEnumerator FadeOutBackgroundAndStop(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            _musicBus.setVolume(_musicVolume * t);
            _ambienceBus.setVolume(_ambienceVolume * t);
            yield return null;
        }

        StopAndRelease(ref _musicEventInstance);
        StopAndRelease(ref _ambienceEventInstance);
        _musicBus.setVolume(_musicVolume);
        _ambienceBus.setVolume(_ambienceVolume);
        _backgroundAudioStarted = false;
    }

    // Stop then release in the correct FMOD order.
    // Calling release() before stop() leaves the event playing until FMOD's GC.
    private static void StopAndRelease(ref EventInstance instance)
    {
        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
    }

    #endregion
}