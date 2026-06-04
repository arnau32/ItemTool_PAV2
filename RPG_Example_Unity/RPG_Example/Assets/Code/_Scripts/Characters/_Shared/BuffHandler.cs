using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Applies and tracks BuffEffects on a character. Four modes:
//   InstantHeal        — one-shot heal
//   HealOverTime       — heals baseValue HP every second for duration seconds
//   ModifyStat         — adds a StatModifier to any CharacterStat for duration seconds
//   ModifyStaminaRegen — additively boosts stamina recovery (units/s) for duration seconds
[RequireComponent(typeof(CharacterStats))]
public class BuffHandler : MonoBehaviour
{
    #region Fields

    [SerializeField] private CharacterStats _stats;
    [SerializeField] private CharacterHealthSystem _health;
    [SerializeField] private StaminaSystem _stamina;

    private readonly Dictionary<object, Coroutine> _activeCoroutines = new();
    private readonly List<Coroutine> _coroutineValues = new();

    // Tracks the total regen delta currently active so RemoveAllBuffs can undo it cleanly.
    private float _activeStaminaRegenDelta;

    #endregion

    #region Events

    // Fires on the player's BuffHandler when a heal buff is applied.
    // Passes the total expected heal (instant = baseValue, HoT = baseValue * duration).
    // Listened to by PlayerHealthBarController to drive the forward heal ghost bar.
    public static event Action<float> OnPlayerHealStarted;

    #endregion

    #region Public API

    public void ApplyBuff(BuffEffect buff, object source)
    {
        switch (buff.applicationMode)
        {
            case Enums.BuffApplicationMode.InstantHeal:
                NotifyHealPreview(buff.baseValue);
                ApplyInstantHeal(buff);
                break;

            case Enums.BuffApplicationMode.HealOverTime:
                NotifyHealPreview(buff.baseValue * buff.duration);
                ApplyHealOverTime(buff, source);
                break;

            case Enums.BuffApplicationMode.ModifyStat:
                ApplyStatBuff(buff, source);
                break;

            case Enums.BuffApplicationMode.ModifyStaminaRegen:
                ApplyStaminaRegenBuff(buff, source);
                break;
        }
    }

    // Only fires for the player's own BuffHandler to avoid triggering on enemy heals.
    private void NotifyHealPreview(float totalHeal)
    {
        if (_health is PlayerHealthSystem)
            OnPlayerHealStarted?.Invoke(totalHeal);
    }

    public void RemoveAllBuffs()
    {
        for (int i = 0; i < _coroutineValues.Count; i++)
        {
            if (_coroutineValues[i] != null)
                StopCoroutine(_coroutineValues[i]);
        }

        _activeCoroutines.Clear();
        _coroutineValues.Clear();
        _stats.RemoveModifiersFromSource(this);

        if (_stamina != null && _activeStaminaRegenDelta != 0f)
        {
            _stamina.SetRecoveryAmountPerSecond(_stamina.RecoveryAmountPerSecond - _activeStaminaRegenDelta);
            _activeStaminaRegenDelta = 0f;
        }
    }

    #endregion

    #region Heal

    private void ApplyInstantHeal(BuffEffect buff)
    {
        if (_health == null)
        {
            Debug.LogWarning($"[BuffHandler] {name} has no CharacterHealthSystem — InstantHeal skipped.");
            return;
        }

        _health.Heal(buff.baseValue);
    }

    private void ApplyHealOverTime(BuffEffect buff, object source)
    {
        if (_health == null)
        {
            Debug.LogWarning($"[BuffHandler] {name} has no CharacterHealthSystem — HealOverTime skipped.");
            return;
        }

        CancelActiveCoroutine(source);

        var coroutine = StartCoroutine(HealOverTimeRoutine(buff, source));
        TrackCoroutine(source, coroutine);
    }

    private IEnumerator HealOverTimeRoutine(BuffEffect buff, object source)
    {
        float elapsed = 0f;
        var tick = new WaitForSeconds(1f);

        while (elapsed < buff.duration)
        {
            yield return tick;
            elapsed += 1f;
            _health.Heal(buff.baseValue);
        }

        UntrackCoroutine(source);
    }

    #endregion

    #region Stat Modifier

    private void ApplyStatBuff(BuffEffect buff, object source)
    {
        if (_activeCoroutines.TryGetValue(source, out var existing))
        {
            StopCoroutine(existing);
            UntrackCoroutine(source);
            RemoveStatModifier(buff, source);
        }

        var modifier = new StatModifier(buff.baseValue, buff.modifierType, (int)buff.modifierType, source, buff.statType);
        buff.runtimeModifier = modifier;

        float prevMax = SnapshotMaxHealthIfNeeded(buff);
        _stats.AddModifier(buff.statType, modifier);
        NotifyHealthIfNeeded(buff, prevMax);

        if (buff.duration > 0f)
        {
            var coroutine = StartCoroutine(StatBuffExpiryRoutine(buff, source));
            TrackCoroutine(source, coroutine);
        }
    }

    private IEnumerator StatBuffExpiryRoutine(BuffEffect buff, object source)
    {
        yield return new WaitForSeconds(buff.duration);
        RemoveStatModifier(buff, source);
        UntrackCoroutine(source);
    }

    private void RemoveStatModifier(BuffEffect buff, object source)
    {
        if (buff.runtimeModifier == null) return;

        float prevMax = SnapshotMaxHealthIfNeeded(buff);
        _stats.RemoveModifier(buff.statType, buff.runtimeModifier);
        NotifyHealthIfNeeded(buff, prevMax);
    }

    private float SnapshotMaxHealthIfNeeded(BuffEffect buff)
    {
        if (buff.statType != Enums.StatType.Health) return 0f;
        return _health != null ? _health.MaxHealth : 0f;
    }

    private void NotifyHealthIfNeeded(BuffEffect buff, float previousMax)
    {
        if (buff.statType != Enums.StatType.Health) return;
        if (_health == null) return;
        _health.NotifyMaxHealthChanged(previousMax);
    }

    #endregion

    #region Stamina Regen

    private void ApplyStaminaRegenBuff(BuffEffect buff, object source)
    {
        if (_stamina == null)
        {
            Debug.LogWarning($"[BuffHandler] {name} has no StaminaSystem — ModifyStaminaRegen skipped.");
            return;
        }

        // Delta approach: additive stacking, safe removal without knowing base value.
        float delta = buff.modifierType == StatModifierType.PercentAdd
            ? _stamina.RecoveryAmountPerSecond * buff.baseValue
            : buff.baseValue;

        // If same source is already active, undo its previous delta before applying the new one.
        if (_activeCoroutines.TryGetValue(source, out var existing))
        {
            StopCoroutine(existing);
            UntrackCoroutine(source);

            if (buff.runtimeRegenDelta != 0f)
            {
                _stamina.SetRecoveryAmountPerSecond(_stamina.RecoveryAmountPerSecond - buff.runtimeRegenDelta);
                _activeStaminaRegenDelta -= buff.runtimeRegenDelta;
            }
        }

        buff.runtimeRegenDelta = delta;
        _stamina.SetRecoveryAmountPerSecond(_stamina.RecoveryAmountPerSecond + delta);
        _activeStaminaRegenDelta += delta;

        if (buff.duration > 0f)
        {
            var coroutine = StartCoroutine(StaminaRegenExpiryRoutine(buff, source));
            TrackCoroutine(source, coroutine);
        }
    }

    private IEnumerator StaminaRegenExpiryRoutine(BuffEffect buff, object source)
    {
        yield return new WaitForSeconds(buff.duration);

        if (_stamina != null)
        {
            _stamina.SetRecoveryAmountPerSecond(_stamina.RecoveryAmountPerSecond - buff.runtimeRegenDelta);
            _activeStaminaRegenDelta -= buff.runtimeRegenDelta;
        }

        buff.runtimeRegenDelta = 0f;
        UntrackCoroutine(source);
    }

    #endregion

    #region Coroutine Tracking

    private void CancelActiveCoroutine(object source)
    {
        if (!_activeCoroutines.TryGetValue(source, out var existing)) return;
        if (existing != null) StopCoroutine(existing);
        UntrackCoroutine(source);
    }

    private void TrackCoroutine(object source, Coroutine coroutine)
    {
        _activeCoroutines[source] = coroutine;
        _coroutineValues.Add(coroutine);
    }

    private void UntrackCoroutine(object source)
    {
        if (!_activeCoroutines.TryGetValue(source, out var coroutine)) return;
        _activeCoroutines.Remove(source);
        _coroutineValues.Remove(coroutine);
    }

    #endregion
}
