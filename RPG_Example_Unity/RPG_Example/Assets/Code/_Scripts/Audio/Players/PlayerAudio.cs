using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

// Handles only audio that cannot live in a FeedbackContainer:
//   - Footstep loop (surface-aware FMOD instance, driven by Animation Events)
//   - Land (Animation Event)
// Everything else (hurt, death, heal, dodge, parry, stamina) has migrated
// to PlayerFeedbacksController where it lives alongside camera and VFX effects.
public class PlayerAudio : MonoBehaviour
{
    #region Fields

    [SerializeField] private PlayerSounds _sounds;
    [SerializeField] private GroundSurfaceDetector _surfaceDetector;
    [SerializeField] private PlayerMovement _movement;

    private AudioService _audioService;
    private EventInstance _footstepInstance;
    private bool _footstepInstanceCreated;

    private const string TerrainParam = "Terrain";
    private const string WalkRunParam = "WalkRun";

    [SerializeField] private float _runThreshold = 0.85f;

    #endregion

    #region Unity Callbacks

    private void Awake() => _audioService = GameServices.Get<AudioService>();

    private void Start()
    {
        _footstepInstance = _audioService.CreateInstance(_sounds.Footstep);
        _footstepInstanceCreated = true;
    }

    private void OnDestroy()
    {
        if (!_footstepInstanceCreated) return;
        _footstepInstance.stop(STOP_MODE.IMMEDIATE);
        _footstepInstance.release();
    }

    #endregion

    #region Public API

    public void Initialize(PlayerContext ctx)
    {
        // Footstep loop needs movement reference — no other initialization required.
        // Health, stamina, dodge, and parry audio are handled by PlayerFeedbacksController.
    }

    public void PlayParryAttempt() => _audioService.PlayOneShot(_sounds.ParryAttempt, transform.position);

    #endregion

    #region Animation Events

    // Add an Animation Event named "OnFootstepEvent" on every foot-contact
    // frame in walk and run animations. The Animator calls this automatically.
    private void OnFootstepEvent()
    {
        if (!_footstepInstanceCreated) return;

        if (_surfaceDetector != null)
            _footstepInstance.setParameterByName(TerrainParam, _surfaceDetector.DetectSurface());

        _footstepInstance.setParameterByName(WalkRunParam, GetWalkRunValue());
        _footstepInstance.set3DAttributes(transform.position.To3DAttributes());
        _footstepInstance.start();
        _footstepInstance.keyOff();
    }

    private void OnLandEvent() => _audioService.PlayOneShot(_sounds.Land, transform.position);

    #endregion

    #region Helpers

    private float GetWalkRunValue()
    {
        float normalized = _movement.CurrentPlanarSpeed / Mathf.Max(_movement.RunSpeed, 0.01f);
        return normalized > _runThreshold ? 1f : 0f;
    }

    #endregion
}