using UnityEngine;
using FeedbacksNagu;
// Player-side feedback hub.
// Every action that produces camera, audio, VFX, or rumble goes through here.
// Stamina exhausted fires once per debt cycle — the guard lives here alongside
// the subscription so no external system needs to manage it.
public class PlayerFeedbacksController : MonoBehaviour
{
    #region Fields

    [Header("Combat")] [SerializeField] private FeedbackContainer _feedbackHitConfirm;

    [Header("Damage & Death")] [SerializeField]
    private FeedbackContainer _feedbackHurt;

    [SerializeField] private FeedbackContainer _feedbackDeath;
    [SerializeField] private FeedbackContainer _feedbackHeal;

    [Header("Parry")] [SerializeField] private FeedbackContainer _feedbackParryAttempt;
    [SerializeField] private FeedbackContainer _feedbackParryNormal;
    [SerializeField] private FeedbackContainer _feedbackParryPerfect;

    [Header("Dodge")] [SerializeField] private FeedbackContainer _feedbackDodge;

    [Header("Sprint")] [SerializeField] private FeedbackContainer _feedbackSprintStart;
    [SerializeField] private FeedbackContainer _feedbackSprintStop;

    [Header("Stamina")] [SerializeField] private FeedbackContainer _feedbackStaminaExhausted;

    [Header("Ability")]
    [SerializeField] private FeedbackContainer _feedbackAbilityReady;
    [SerializeField] private GameObject _abilityReadyVisual;

    private VfxPoolService _vfxPool;
    private StaminaSystem _staminaSystem;

    // Guard: fires _feedbackStaminaExhausted once per debt cycle, not every frame.
    private bool _staminaDebtFeedbackPlayed;

    // When true, sprint feedbacks are suppressed (e.g. inside a ViewerVillage zone).
    private bool _viewerModeActive;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        GameServices.TryGet(out _vfxPool);
    }

private void OnDisable()
    {
        UnsubscribeStamina();
        AbilityScoreSystem.OnAbilityReady -= PlayAbilityReady;
        AbilityScoreSystem.OnAbilityUsed  -= StopAbilityReady;
    }

    private void OnDestroy()
    {
        UnsubscribeStamina();
        AbilityScoreSystem.OnAbilityReady -= PlayAbilityReady;
        AbilityScoreSystem.OnAbilityUsed  -= StopAbilityReady;
    }

    #endregion

    #region Public API

    // Called by PlayerController.ExecuteDeferredInitializations once context is ready.
    public void Initialize(StaminaSystem staminaSystem)
    {
        UnsubscribeStamina();

        _staminaSystem = staminaSystem;
        _staminaDebtFeedbackPlayed = false;

        if (_staminaSystem != null)
            _staminaSystem.OnStaminaModifies += HandleStaminaModified;

        AbilityScoreSystem.OnAbilityReady -= PlayAbilityReady;
        AbilityScoreSystem.OnAbilityReady += PlayAbilityReady;

        AbilityScoreSystem.OnAbilityUsed -= StopAbilityReady;
        AbilityScoreSystem.OnAbilityUsed += StopAbilityReady;

        // Ability starts at 0 — ensure the visual is off regardless of prefab state.
        StopAbilityReady();
    }

    public void PlayAbilityReady()
    {
        _feedbackAbilityReady?.PlayFeedbacks(gameObject);
        if (_abilityReadyVisual != null) _abilityReadyVisual.SetActive(true);
    }

    public void StopAbilityReady()
    {
        if (_abilityReadyVisual != null) _abilityReadyVisual.SetActive(false);
    }
    public void PlayHitConfirm() => _feedbackHitConfirm?.PlayFeedbacks(gameObject);
    public void PlayHurt() => _feedbackHurt?.PlayFeedbacks(gameObject);
    public void PlayDeath() => _feedbackDeath?.PlayFeedbacks(gameObject);
    public void PlayHeal() => _feedbackHeal?.PlayFeedbacks(gameObject);
    public void SetViewerMode(bool active) => _viewerModeActive = active;

    public void PlaySprintStart()
    {
        if (_viewerModeActive) return;
        _feedbackSprintStart?.PlayFeedbacks(gameObject);
    }

    public void PlaySprintStop()
    {
        if (_viewerModeActive) return;
        _feedbackSprintStop?.PlayFeedbacks(gameObject);
    }
    public void PlayParryAttempt() => _feedbackParryAttempt?.PlayFeedbacks(gameObject);
    public void PlayDodge() => _feedbackDodge?.PlayFeedbacks(gameObject);

    // Routes to Normal or Perfect container based on parry quality.
    public void PlayParryReceived(Vector3 hitPoint, Quaternion rotation, Enums.ParryQuality quality)
    {
        if (quality == Enums.ParryQuality.Perfect)
            _feedbackParryPerfect?.PlayFeedbacks(gameObject);
        else
            _feedbackParryNormal?.PlayFeedbacks(gameObject);
    }

    #endregion

    #region Stamina Debt

    private void HandleStaminaModified()
    {
        if (_staminaSystem == null) return;

        if (_staminaSystem.IsInDebt)
        {
            // Fire only once per debt cycle — not every frame while draining.
            if (_staminaDebtFeedbackPlayed) return;

            _staminaDebtFeedbackPlayed = true;
            _feedbackStaminaExhausted?.PlayFeedbacks(gameObject);
        }
        else
        {
            // Reset flag when stamina recovers so the next debt cycle fires again.
            _staminaDebtFeedbackPlayed = false;
        }
    }

    private void UnsubscribeStamina()
    {
        if (_staminaSystem == null) return;

        _staminaSystem.OnStaminaModifies -= HandleStaminaModified;
        _staminaSystem = null;
    }

    #endregion
}