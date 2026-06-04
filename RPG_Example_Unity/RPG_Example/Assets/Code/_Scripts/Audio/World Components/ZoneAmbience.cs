using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// Attach to any zone trigger (BoxCollider with isTrigger=true, or similar).
// Plays zone-specific ambient loops when the player enters the zone.
//
// Examples:
//   River zone   → EventReference = event:/Ambience/River
//   Forest zone  → EventReference = event:/Ambience/Forest_Birds
public class ZoneAmbience : TriggerPlayer
{
    [SerializeField] private EventReference _zoneAmbienceEvent;

    private EventInstance _instance;
    private bool _initialized;
    private bool _playerInZone;
    
    #region Unity Callbacks
    
    private void Awake()
    {
        if (_zoneAmbienceEvent.IsNull)
        {
            Debug.LogWarning($"[ZoneAmbience] EventReference not set on {gameObject.name}.", this);
            return;
        }

        _instance = RuntimeManager.CreateInstance(_zoneAmbienceEvent);
        _initialized = true;

        RuntimeManager.AttachInstanceToGameObject(_instance, gameObject);
    }
    
    private void OnDestroy()
    {
        if (!_initialized) return;
        _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _instance.release();
    }
    
    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (!_initialized || _playerInZone) return;
        
        _playerInZone = true;
        _instance.start();
        
        // Optionally fade in via FMOD timeline or an AHDSR parameter.
        // If you have a "FadeIn" parameter in FMOD, drive it here:
        // _instance.setParameterByName("FadeIn", 1f);
    }
    
    protected override void OnPlayerTriggerExit(Collider other)
    {
        if (!_initialized || !_playerInZone) return;
        
        _playerInZone = false;

        // AllowFadeout lets FMOD's release tail finish naturally.
        _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

    }
    
    #endregion

    #region Public API

    // Call this if you want to force-enable ambience without trigger collision (e.g. cutscenes, scripted level transitions).
    public void ForceStart()
    {
        if (!_initialized) return;
        _playerInZone = true;
        _instance.start();
    }

    public void ForceStop()
    {
        if (!_initialized) return;
        _playerInZone = false;
        _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    // Drive FMOD parameters at runtime (e.g. rain intensity in a storm zone).
    public void SetParameter(string paramName, float value)
    {
        if (!_initialized) return;
        _instance.setParameterByName(paramName, value);
    }

    #endregion
   
}