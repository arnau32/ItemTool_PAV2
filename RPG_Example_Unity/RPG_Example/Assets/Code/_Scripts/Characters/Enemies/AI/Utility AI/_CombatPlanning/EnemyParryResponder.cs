using System;
using UnityEngine;

/// Handles incoming parry attempts against the enemy's active attack.
/// Extracted from EnemyCombatPlanner to isolate the parry responsibility.
public sealed class EnemyParryResponder
{
    #region Fields

    private const float PARRY_CONSUME_GRACE_SECONDS = 0.15f;

    private bool _parriedThisAttack;
    private float _consumeHitsUntil;
    private PlayerCombatController _cachedPlayerCombat;

    public event Action<Enums.ParryQuality> OnParried;

    #endregion

    #region Public API

    public void Reset()
    {
        _parriedThisAttack = false;
        _consumeHitsUntil = 0f;
    }

    public void OnAttackStarted()
    {
        _parriedThisAttack = false;
    }

    // Attempts to consume a hit as a parry.
    // Returns a result indicating whether the hit was consumed and whether it was a fresh parry.
    public EnemyCombatPlanner.ParryConsumeResult TryConsume(Component target, EnemyAttackRuntime attackRuntime, Animator animator)
    {
        float now = Time.time;

        if (now < _consumeHitsUntil)
        {
            return EnemyCombatPlanner.ParryConsumeResult.ConsumedNoFx();
        }

        if (!attackRuntime.IsActive || attackRuntime.ActiveAttack == null)
        {
            return EnemyCombatPlanner.ParryConsumeResult.No();
        }

        if (target == null) return EnemyCombatPlanner.ParryConsumeResult.No();
        if (_parriedThisAttack) return EnemyCombatPlanner.ParryConsumeResult.ConsumedNoFx();

        if (_cachedPlayerCombat == null)
            _cachedPlayerCombat = target.GetComponentInParent<PlayerCombatController>();
        if (_cachedPlayerCombat == null) return EnemyCombatPlanner.ParryConsumeResult.No();

        var playerCombat = _cachedPlayerCombat;

        if (attackRuntime.HasHitTarget(playerCombat.GetInstanceID()))
        {
            return EnemyCombatPlanner.ParryConsumeResult.No();
        }

        var (parryActive, quality) = playerCombat.GetParryState();
        if (!parryActive) return EnemyCombatPlanner.ParryConsumeResult.No();

        attackRuntime.TryGetCurrentNormalizedTime(animator, out float tNorm);

        if (!IsActiveAttackParryableNow(attackRuntime.ActiveAttack, tNorm))
        {
            playerCombat.NotifyParryResult(false);
            return EnemyCombatPlanner.ParryConsumeResult.No();
        }

        playerCombat.NotifyParryResult(true);

        _parriedThisAttack = true;
        _consumeHitsUntil = now + PARRY_CONSUME_GRACE_SECONDS;

        attackRuntime.ForceStop();

        OnParried?.Invoke(quality);
        return EnemyCombatPlanner.ParryConsumeResult.Fresh(quality);
    }

    #endregion

    #region Helpers

    private static bool IsActiveAttackParryableNow(AttackData attack, float tNorm)
    {
        if (!attack.allowCanBeParriedWindow) return false;

        var windows = attack.canBeParriedWindows;
        if (windows == null || windows.Count == 0) return false;

        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].IsActive(tNorm)) return true;
        }

        return false;
    }

    #endregion
}