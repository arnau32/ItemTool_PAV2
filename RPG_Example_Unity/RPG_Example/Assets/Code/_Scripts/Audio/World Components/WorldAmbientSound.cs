using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// Positional audio for any world object: campfires, waterfalls, doors,
// levers, chests, wind zones, magic circles...
//
// TWO modes:
//   AutoPlay = true  → looping ambient (campfire, waterfall). Starts on Awake.
//   AutoPlay = false → triggered (door creak, chest open). Call Play() manually.
//
// Setup for door:
//   1. Add WorldAmbientSound to the door GO.
//   2. AutoPlay = false.
//   3. From DoorController: GetComponent<WorldAmbientSound>().Play();
public class WorldAmbientSound : MonoBehaviour
{
    [SerializeField] private EventReference _eventReference;
    [SerializeField] private bool _autoPlay = true;

    private EventInstance _instance;
    private bool _initialized;

    #region Unity Callbacks

    private void Awake()
    {
        if (_eventReference.IsNull)
        {
            Debug.LogWarning($"[WorldAmbientSound] EventReference not set on {gameObject.name}.", this);
            return;
        }

        _instance = RuntimeManager.CreateInstance(_eventReference);

        // AttachInstanceToGameObject updates the FMOD 3D position every frame
        RuntimeManager.AttachInstanceToGameObject(_instance, gameObject);

        _initialized = true;

        if (_autoPlay) Play();
    }

    private void OnDestroy()
    {
        if (!_initialized) return;
        _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _instance.release();
    }

    #endregion

    #region Public API

    public void Play()
    {
        if (!_initialized) return;
        _instance.start();
    }

    public void Stop(bool immediate = false)
    {
        if (!_initialized) return;
        _instance.stop(immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    // Drive FMOD parameters at runtime.
    // Example: fire intensity based on wind speed.
    //   _worldSound.SetParameter("Intensity", windSpeed01);
    public void SetParameter(string paramName, float value)
    {
        if (!_initialized) return;
        _instance.setParameterByName(paramName, value);
    }

    public void SetPaused(bool paused)
    {
        if (!_initialized) return;
        _instance.setPaused(paused);
    }

    #endregion

   
}