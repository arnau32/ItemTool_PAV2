using System;
using UnityEngine;

// Tracks accumulated poise damage: 0 = no damage taken, 1 = poise broken (stun).
// The bar fills as the enemy absorbs hits — reaching 1 triggers OnPoiseBreak.
// After stun ends the bar resets to 0 and slowly drains back during combat.
public class EnemyPoiseSystem
{
    #region Fields

    private float _maxPoise;
    private float _accumulatedDamage;

    private float _regenDelay;
    private float _regenRate;

    private float _timeSinceLastHit;
    private bool _isBroken;

    #endregion

    #region Properties

    // 0 = undamaged, 1 = broken. Bar fills as hits land.
    public float Poise01 => _maxPoise > 0f ? Mathf.Clamp01(_accumulatedDamage / _maxPoise) : 0f;
    public bool IsBroken => _isBroken;

    // Fired when poise value changes due to damage, break, or reset.
    // Not fired during Tick regen to avoid per-frame event spam — the bar
    // polls Poise01 directly via LateUpdate for smooth drain.
    public event Action OnPoiseChanged;
    public event Action OnPoiseBreak;

    #endregion

    #region Public API

    public void Initialize(float maxPoise, float regenDelay, float regenRate)
    {
        _maxPoise = maxPoise;
        _regenDelay = regenDelay;
        _regenRate = regenRate;

        Reset();
    }

    public void Reset()
    {
        _accumulatedDamage = 0f;
        _timeSinceLastHit = 0f;
        _isBroken = false;

        OnPoiseChanged?.Invoke();
    }

    public void TakePoiseDamage(float amount)
    {
        if (_isBroken || amount <= 0f) return;

        _accumulatedDamage = Mathf.Min(_accumulatedDamage + amount, _maxPoise);
        _timeSinceLastHit = 0f;

        OnPoiseChanged?.Invoke();

        if (_accumulatedDamage >= _maxPoise)
        {
            _isBroken = true;
            OnPoiseBreak?.Invoke();
        }
    }

    // Called by StunState.OnExit — resets bar to 0 so it can be filled again.
    public void NotifyStunEnded()
    {
        _accumulatedDamage = 0f;
        _timeSinceLastHit = 0f;
        _isBroken = false;
        OnPoiseChanged?.Invoke();
    }

    public void Tick(float dt)
    {
        if (_isBroken) return;
        if (_accumulatedDamage <= 0f) return;

        _timeSinceLastHit += dt;

        if (_timeSinceLastHit < _regenDelay) return;

        // Bar drains back toward 0 over time.
        _accumulatedDamage = Mathf.Max(0f, _accumulatedDamage - _regenRate * dt);

        if (_accumulatedDamage <= 0f)
        {
            _timeSinceLastHit = 0f;
        }
    }

    #endregion
}