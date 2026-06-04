using System;
using UnityEngine;

public class CharacterHealthSystem : MonoBehaviour, IDamageable, IHealth
{
    #region Fields

    protected CharacterStats _stats;
    protected CharacterStat _healthStat;
    protected CharacterStat _defenseStat;
    protected float _currentHealth;
    protected bool _isInvulnerable;

    [Header("Defense Formula")]
    [Tooltip("Hard cap on damage reduction. 0.50 = enemy/player can never absorb more than 50% of any hit.")]
    [Range(0f, 0.95f), SerializeField] private float _reductionCap = 0.50f;

    [Tooltip("Controls how fast defense reaches the cap. Higher = defense weaker overall. " +
             "At K=100 you need Defense=100 to reach the cap.")]
    [SerializeField] private float _defenseK = 100f;

    #endregion

    #region Properties

    public bool IsInvulnerable => _isInvulnerable;

    public float CurrentHealth01
    {
        get
        {
            float max = MaxHealth;
            return max <= 0f ? 0f : Mathf.Clamp01(_currentHealth / max);
        }
    }

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _healthStat != null ? _healthStat.Value : 0f;
    public bool IsAlive => _currentHealth > 0;

    #endregion

    #region Events

    public event Action<float> OnHealthChanged;
    public event Action<float> OnHeal;

    // poiseDamage is passed through so EnemyBase.Damaged can apply it to the poise system
    // without requiring a GetComponent or interface cast at hit time.
    public event Action<float, Enums.HitType, float> OnDamageTaken;
    public event Action OnDeath;
    public event Action<float> OnMaxHealthChanged;

    #endregion

    #region Public API

    public void Initialize(CharacterStats stats)
    {
        SetupStats(stats);
    }

    public void Respawn(CharacterStats stats)
    {
        SetupStats(stats);
        OnHealthChanged?.Invoke(CurrentHealth01);
    }

    public virtual void TakeDamage(float damage, Enums.HitType type)
        => ApplyDamage(damage, Vector3.zero, Vector3.zero, type, 0f);

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type)
        => ApplyDamage(damage, hitPoint, direction, type, 0f);

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type, float poiseDamage)
        => ApplyDamage(damage, hitPoint, direction, type, poiseDamage);

    // Static overload used by the auto balance tool — pass K and cap explicitly.
    public static float CalculateDamage(float rawDamage, float defense, float defenseK, float reductionCap)
    {
        float reduction = Mathf.Min(defense / (defense + defenseK), reductionCap);
        return Mathf.Max(rawDamage * (1f - reduction), 1f);
    }

    #endregion

    protected virtual void ApplyDamage(float rawDamage, Vector3 hitPoint, Vector3 direction, Enums.HitType type, float poiseDamage)
    {
        if (_isInvulnerable || !IsAlive) return;

        float defense     = _defenseStat != null ? _defenseStat.Value : 0f;
        float finalDamage = CalculateDamage(rawDamage, defense, _defenseK, _reductionCap);

        _currentHealth = Mathf.Max(_currentHealth - finalDamage, 0f);

        DamageCustomEffects();

        OnDamageTaken?.Invoke(finalDamage, type, poiseDamage);
        OnHealthChanged?.Invoke(CurrentHealth01);

        if (_currentHealth <= 0f)
            Die();
    }

    protected void RaiseOnDamageTaken(float damage, Enums.HitType type, float poiseDamage)
        => OnDamageTaken?.Invoke(damage, type, poiseDamage);

    protected void RaiseOnHealthChanged()
        => OnHealthChanged?.Invoke(CurrentHealth01);

    public virtual void DamageCustomEffects() { }

    public virtual void Heal(float amount)
    {
        float max = MaxHealth;
        if (max <= 0f) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, max);
        OnHeal?.Invoke(amount);
        OnHealthChanged?.Invoke(CurrentHealth01);
    }

    public void SetCurrentHealth(float value)
    {
        float max = MaxHealth;
        _currentHealth = value < 0f ? max : Mathf.Clamp(value, 0f, max);
        OnHealthChanged?.Invoke(CurrentHealth01);
    }

    public void SetInvulnerable(bool invulnerable) => _isInvulnerable = invulnerable;

    public void NotifyMaxHealthChanged(float previousMax)
    {
        float newMax = MaxHealth;
        if (newMax <= 0f) return;

        if (previousMax <= 0f)
        {
            _currentHealth = newMax;
        }
        else
        {
            bool wasAtFullHealth = (_currentHealth >= previousMax - 0.5f);
            _currentHealth = wasAtFullHealth ? newMax : Mathf.Clamp(_currentHealth, 0f, newMax);
        }

        OnMaxHealthChanged?.Invoke(CurrentHealth01);
        OnHealthChanged?.Invoke(CurrentHealth01);
    }

    private void SetupStats(CharacterStats stats)
    {
        _stats = stats;
        if (_stats != null)
        {
            _healthStat    = _stats.GetStat(Enums.StatType.Health);
            _defenseStat   = _stats.GetStat(Enums.StatType.Defense);
            _currentHealth = MaxHealth;
        }
        else
        {
            _healthStat    = null;
            _defenseStat   = null;
            _currentHealth = 0f;
        }

        _isInvulnerable = false;
    }

    protected virtual void Die()
    {
        OnDeath?.Invoke();
    }
}
