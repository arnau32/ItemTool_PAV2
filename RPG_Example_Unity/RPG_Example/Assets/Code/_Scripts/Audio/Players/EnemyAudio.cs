using UnityEngine;
using FMOD.Studio;

// Handles only audio that cannot live in a FeedbackContainer:
//   - Footstep loop (surface-aware FMOD instance, driven by Animation Events)
//   - Alert / Suspicious barks (cooldown-gated, need state awareness)
//   - Idle vocalizations
// Hurt and Death have migrated to EnemyFeedbacksController._feedbackHurt / _feedbackDeath.
public class EnemyAudio : MonoBehaviour
{
    #region Fields

    [SerializeField] private EnemySounds _sounds;
    [SerializeField] private GroundSurfaceDetector _surfaceDetector;

    private AudioService _audioService;
    private EventInstance _footstepInstance;
    private bool _footstepInstanceCreated;

    private const float AlertCooldown = 4f;
    private const float SuspiciousCooldown = 3f;

    private float _lastAlertTime = -999f;
    private float _lastSuspiciousTime = -999f;

    private const string SurfaceParam = "Surface";

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _audioService = GameServices.Get<AudioService>();

        if (_sounds == null)
            Debug.LogError($"[EnemyAudio] EnemySounds not assigned on {gameObject.name}.", this);
    }

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

    #region Animation Events

    private void OnFootstepEvent()
    {
        if (!_footstepInstanceCreated) return;

        if (_surfaceDetector != null)
        {
            float surfaceValue = _surfaceDetector.DetectSurface();
            _footstepInstance.setParameterByName(SurfaceParam, surfaceValue);
        }

        _footstepInstance.start();
        _footstepInstance.keyOff();
    }

    #endregion

    #region Public API

    public void PlayHitReaction() => _audioService.PlayOneShot(_sounds.HitReaction, transform.position);

    public void PlayAlert()
    {
        if (Time.time - _lastAlertTime < AlertCooldown) return;

        _lastAlertTime = Time.time;
        _audioService.PlayOneShot(_sounds.Alert, transform.position);
    }

    /// Plays a "what was that?" sound when the enemy becomes Suspicious.
    /// Uses a separate cooldown from the full alert bark.
    public void PlaySuspicious()
    {
        if (Time.time - _lastSuspiciousTime < SuspiciousCooldown) return;

        _lastSuspiciousTime = Time.time;
        _audioService.PlayOneShot(_sounds.Idle, transform.position);
    }

    public void PlayIdle() => _audioService.PlayOneShot(_sounds.Idle, transform.position);

    #endregion
}