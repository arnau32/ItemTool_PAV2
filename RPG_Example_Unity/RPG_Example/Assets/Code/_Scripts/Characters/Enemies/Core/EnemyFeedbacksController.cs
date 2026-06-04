using UnityEngine;
using FeedbacksNagu;

// Enemy-side feedback hub, mirroring PlayerFeedbacksController.
// Owned by EnemyBase and exposed through EnemyContext so states and systems
// can trigger feedbacks without coupling to MonoBehaviour directly.
//
// Hurt and Death subscribe to the health system via Initialize() so they fire
// automatically — no call site needed in EnemyAudio.
public class EnemyFeedbacksController : MonoBehaviour
{
    #region Fields

    [Header("Damage & Death")] 
    [SerializeField] private FeedbackContainer _feedbackHurt;
    [SerializeField] private FeedbackContainer _feedbackDeath;

    [Header("Spawn")]
    [SerializeField] private FeedbackContainer _feedbackSpawn;

    [Header("Poise")]
    [SerializeField] private FeedbackContainer _feedbackPoiseBreak;
    [SerializeField] private FeedbackContainer _feedbackStunEnter;

    private VfxPoolService _vfxPool;
    private CharacterHealthSystem _healthSystem;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        GameServices.TryGet(out _vfxPool);
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    private void OnDestroy()
    {
        UnsubscribeHealth();
    }

    #endregion

    #region Public API

    // Called by EnemyBase.Awake after the health system is available.
    public void Initialize(CharacterHealthSystem healthSystem)
    {
        UnsubscribeHealth();

        _healthSystem = healthSystem;

        if (_healthSystem == null) return;

        _healthSystem.OnDamageTaken += HandleDamageTaken;
        _healthSystem.OnDeath += HandleDeath;
    }

    // Called from SpawnState.OnEnter — fires when the spawn animation begins.
    public void PlaySpawn()
    {
        _feedbackSpawn.PlayFeedbacks(gameObject);
    }

    // Called from EnemyBase.HandlePoiseBreak — fires once when poise bar fills completely.
    public void PlayPoiseBreak()
    {
        _feedbackPoiseBreak.PlayFeedbacks(gameObject);
    }

    // Called from StunState.OnEnter — fires when the stun animation begins.
    public void PlayStunEnter()
    {
        _feedbackStunEnter.PlayFeedbacks(gameObject);
    }

    #endregion

    #region Health Event Handlers

    private void HandleDamageTaken(float damage, Enums.HitType hitType, float poiseDamage)
    {
        _feedbackHurt.PlayFeedbacks(gameObject);
    }

    private void HandleDeath()
    {
        _feedbackDeath.PlayFeedbacks(gameObject);
    }

    #endregion

    #region Helpers

    private void UnsubscribeHealth()
    {
        if (_healthSystem == null) return;

        _healthSystem.OnDamageTaken -= HandleDamageTaken;
        _healthSystem.OnDeath -= HandleDeath;
        _healthSystem = null;
    }

    #endregion
}