using System;
using UnityEngine;

public class PlayerLifeController : MonoBehaviour
{
    // Fires the first time (and every time) the player enters knockdown.
    public static event Action OnPlayerKnockdown;

    #region Fields

    [SerializeField] private GameObject temporalUIDead;

    [Header("Knockdown")] [Tooltip("Seconds spent in the loop state before auto get-up triggers (if player doesn't dodge).")] [SerializeField]
    private float _knockdownDodgeDelay = 1.5f;

    private PlayerContext _context;

    private bool _isInKnockback;
    private bool _isInKnockdown;
    private bool _canDodgeInKnockdown;

    #endregion

    #region Properties

    public bool IsInKnockdown => _isInKnockdown;

    // True while any hit-reaction root-motion animation is playing.
    public bool IsInHitReaction => _isInKnockback || _isInKnockdown;

    #endregion

    #region Public API

    public void Initialize(PlayerContext ctx)
    {
        _context = ctx;

        _context.Stamina.OnStaminaModifies += HandleStaminaModifies;
        _context.Health.OnHealthChanged += HandleHealthChanged;
        _context.Health.OnHeal += HandleHeal;
        _context.Health.OnDamageTaken += HandleGetDamage;
        _context.Health.OnDeath += HandleDead;
        _context.Health.OnMaxHealthChanged += HandleHealthChanged;

        _context.Hud.UpdateHealthBar(_context.Health.CurrentHealth01);
        PlayerHealthBarController.Instance?.SetHealthSystem(_context.Health);

        CombatAnalytics.EnsureSession();
        CombatAnalytics.PlayerStaminaChanged(_context.Stamina.CurrentStamina, _context.Stamina.MaxStamina);
    }

    #endregion

    #region Unity Callbacks

    private void OnDisable()
    {
        if (_context == null) return;

        PlayerHealthBarController.Instance?.SetHealthSystem(null);

        _context.Stamina.OnStaminaModifies -= HandleStaminaModifies;
        _context.Health.OnHealthChanged -= HandleHealthChanged;
        _context.Health.OnHeal -= HandleHeal;
        _context.Health.OnDamageTaken -= HandleGetDamage;
        _context.Health.OnDeath -= HandleDead;
        _context.Health.OnMaxHealthChanged -= HandleHealthChanged;

        ForceExitAllReactions();
    }

    #endregion

    #region Event Handlers

    private void HandleStaminaModifies()
    {
        CombatAnalytics.PlayerStaminaChanged(_context.Stamina.CurrentStamina, _context.Stamina.MaxStamina);

        if (_context.Stamina is not StaminaSystem ss) return;

        if (ss.IsInDebt)
        {
            // TODO: track debt events for analytics
        }
    }

    private void HandleHealthChanged(float health01)
    {
        _context.Hud.UpdateHealthBar(health01);
    }

    private void HandleHeal(float amount)
    {
        float max = _context.Health.MaxHealth;
        if (max <= 0f) return;

        CombatAnalytics.PlayerHealed01(amount / max);
        _context.FeedbacksController?.PlayHeal();
    }

    private void HandleGetDamage(float dmg, Enums.HitType hitType, float poiseDamage)
    {
        bool wasInDodgeFrames = _context.Movement.DodgeSystem.IsInDodge;
        CombatAnalytics.PlayerTookDamage(dmg, hitType, wasInDodgeFrames);

        _context.PlayableController?.TryInterrupt(PlayableInterruptFlags.Hit);

        // Knockdown grants invulnerability — suppress all reactions.
        if (_isInKnockdown) return;

        if (_context.Movement.DodgeSystem.IsInIFrames) return;

        // Hyperarmor absorbs all hit reactions (Normal, Knockback, Knockdown).
        // Damage is still applied — only the reaction is suppressed.
        if (_context.ComboController.IsInCombo)
        {
            var ca = _context.ComboController.CurrentAttack;
            if (ca != null && ca.hasHyperArmor) return;
        }

        switch (hitType)
        {
            case Enums.HitType.Normal:
                _context.FeedbacksController?.PlayHurt();
                break;

            case Enums.HitType.Knockback:
                HandleKnockback();
                break;

            case Enums.HitType.Knockdown:
                HandleKnockdown();
                break;

            default:
                Debug.LogWarning($"[PlayerLifeController] Unhandled HitType: {hitType}.", this);
                _context.FeedbacksController?.PlayHurt();
                break;
        }
    }

    private void HandleDead()
    {
        float lostLoot = CalculateTotalLootValue();
        CombatAnalytics.ExtractionRunEnded(false, lostLoot, 0f, "", "", transform.position);
        CombatAnalytics.EndSession("player_death");

        CombatMusicTracker.Instance?.ForceExitOnDeath();

        _context.FeedbacksController?.PlayDeath();
        _context.PlayableController?.ForceInterrupt();
        ForceExitAllReactions();

        _context.Movement.Rigidbody.isKinematic = true;

        if (TabViewManager.Instance != null && TabViewManager.Instance.isActive)
            TabViewManager.Instance.CloseTabView();

        if (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen)
        {
            WorldMapController.Instance.SetMapOpen(false);
            GameServices.Get<InputService>().OnUIMapClose();
        }

        GameServices.Get<InputService>().OnUIOpen();

        _context.Animation.PlayTargetAnimation("Die", 0.2f);

        if (_context.LockOnSystem.IsLockedOn)
            _context.LockOnSystem.ToggleLock();

        if (GameServices.TryGet<SaveService>(out var save))
            save.ApplyDeathPenalty();

        RespawnData.SpawnPointIndex  = 0;
        RespawnData.CameFromDeath    = true;
        RespawnData.UseSavedPosition = false;

        WaitExtension.Wait(1.5f, () =>
        {
            if (GameServices.TryGet<SaveService>(out var s))
                s.SaveImmediate();

            if (temporalUIDead != null)
                temporalUIDead.SetActive(true);
        });
    }

    #endregion

    #region Knockback / Knockdown

    private void HandleKnockback()
    {
        // Use IsInCombo instead of IsAttacking: the animator OnStateEnter fires one frame after
        // Execute(), so IsAttacking can be false while _currentAttack is already set.
        // Hyperarmor is now checked before reaching this method.
        if (_context.ComboController.IsInCombo)
            _context.ComboController.ResetCombo();

        _context.FeedbacksController?.PlayHurt();
        _context.Animation.PlayTargetAnimation("Player_Knockback", 0.15f);

        _isInKnockback = true;

        WaitExtension.WaitForAnimationOnLayer(
            _context.Animation.Animator,
            PlayerAnimHashes.LayerOverride,
            () =>
            {
                // May have been cleared already if dodge cancelled the knockback.
                _isInKnockback = false;
            });
    }

    private void HandleKnockdown()
    {
        // Knockdown always interrupts — no hyperarmor exception.
        // Use IsInCombo for the same reason as HandleKnockback.
        if (_context.ComboController.IsInCombo)
        {
            _context.ComboController.ResetCombo();
        }

        _isInKnockback = false;

        _context.FeedbacksController?.PlayHurt();
        _context.Animation.PlayTargetAnimation("Player_Knockdown", 0.15f);

        OnPlayerKnockdown?.Invoke();

        EnterKnockdown();
    }

    private void EnterKnockdown()
    {
        _isInKnockdown = true;
        _canDodgeInKnockdown = false;

        _context.Inputs.OnDodge -= HandleDodgeInputDuringKnockdown;
        _context.Inputs.OnDodge += HandleDodgeInputDuringKnockdown;

        if (_context.Health is PlayerHealthSystem phs)
            phs.SetInvulnerable(true);

        WaitExtension.WaitForAnimationOnLayer(_context.Animation.Animator, PlayerAnimHashes.LayerOverride, OnKnockdownStartFinished);
    }

    // Called by Animation Event on the last frame of Player_KnockdownStart clip.
    // From this point the player is in the loop and can dodge to recover.
    public void NotifyKnockdownLoopStarted()
    {
        _canDodgeInKnockdown = true;
    }

    private void OnKnockdownStartFinished()
    {
        if (!_isInKnockdown) return;

        _canDodgeInKnockdown = true;
        WaitExtension.Wait(_knockdownDodgeDelay, TryAutoGetUp);
    }

    private void TryAutoGetUp()
    {
        if (!_isInKnockdown) return;

        FireKnockdownDone();
    }

    private void FireKnockdownDone()
    {
        _context.Animation.Animator.SetTrigger(SharedHashes.HashKnockdownDone);

        WaitExtension.WaitForAnimationOnLayer(
            _context.Animation.Animator,
            PlayerAnimHashes.LayerOverride,
            OnKnockdownEndFinished);
    }

    private void OnKnockdownEndFinished()
    {
        ExitKnockdown();
    }

    private void ExitKnockdown()
    {
        if (!_isInKnockdown) return;

        _isInKnockdown = false;

        _context.Inputs.OnDodge -= HandleDodgeInputDuringKnockdown;

        if (_context?.Health is PlayerHealthSystem phs)
            phs.SetInvulnerable(false);
    }

    public void CancelKnockback()
    {
        _isInKnockback = false;
    }

    private void ForceExitAllReactions()
    {
        _isInKnockback = false;
        ExitKnockdown();
    }

    private void HandleDodgeInputDuringKnockdown()
    {
        if (!_canDodgeInKnockdown) return;

        _context.Inputs.OnDodge -= HandleDodgeInputDuringKnockdown;

        ExitKnockdown();

        _context.Movement.DodgeSystem.TryExecuteDodge(_context.LockOnSystem.IsLockedOn, _context.Inputs.GetDirectionNormalized());
    }

    private static float CalculateTotalLootValue()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return 0f;
        float total = 0f;
        var stacks = inv.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data != null)
                total += stacks[i].data.value * stacks[i].quantity;
        }
        return total;
    }

    #endregion
}