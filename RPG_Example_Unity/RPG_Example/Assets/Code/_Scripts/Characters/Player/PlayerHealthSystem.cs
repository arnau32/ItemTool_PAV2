using UnityEngine;

public class PlayerHealthSystem : CharacterHealthSystem
{
    #region Fields

    [Header("Damage UI")]
    public GameObject[] bloodImages = new GameObject[3];
    public GDTFadeEffect fadeOutLowHp;

    [Header("Defense")]
    [Tooltip("Cap total de reducción de daño del player (0.50 = 50%). " +
             "El stat Defense se interpreta como puntos de porcentaje directos: valor 25 = 25% reducción. " +
             "Armor aporta hasta 25%, hoguera aporta hasta 25% — ambas fuentes se suman aquí.")]
    [SerializeField, Range(0f, 1f)] private float _maxTotalDefense = 0.50f;

    #endregion

    #region Properties

    public Vector3 LastHitDirection { get; private set; }

    // Reducción actual (0–1) con la fórmula %. Útil para UI/debug.
    public float CurrentDefenseReduction =>
        Mathf.Clamp((_defenseStat != null ? _defenseStat.Value : 0f) / 100f, 0f, _maxTotalDefense);

    #endregion

    #region TakeDamage overrides

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type)
    {
        LastHitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        base.TakeDamage(damage, hitPoint, direction, type);
    }

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type, float poiseDamage)
    {
        LastHitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        base.TakeDamage(damage, hitPoint, direction, type, poiseDamage);
        // poiseDamage is intentionally ignored — the player has no poise system.
    }

    #endregion

    #region ApplyDamage — % formula

    // Enemies use CharacterHealthSystem.ApplyDamage (K diminishing returns).
    // Players use direct percentage: Defense stat value = reduction in % points (3 → 3%).
    protected override void ApplyDamage(float rawDamage, Vector3 hitPoint, Vector3 direction, Enums.HitType type, float poiseDamage)
    {
        if (_isInvulnerable || !IsAlive) return;

        float defPct      = _defenseStat != null ? _defenseStat.Value : 0f;
        float reduction   = Mathf.Clamp(defPct / 100f, 0f, _maxTotalDefense);
        float finalDamage = Mathf.Max(rawDamage * (1f - reduction), 1f);

        _currentHealth = Mathf.Max(_currentHealth - finalDamage, 0f);

        DamageCustomEffects();

        RaiseOnDamageTaken(finalDamage, type, poiseDamage);
        RaiseOnHealthChanged();

        if (_currentHealth <= 0f)
            Die();
    }

    #endregion

    #region Visual effects

    // Hurt feedback (camera shake, audio, etc.) is handled by PlayerFeedbacksController.PlayHurt()
    // which is called from PlayerLifeController.HandleGetDamage via OnDamageTaken.
    // DamageCustomEffects is kept only for the blood UI which is player-specific visual state.
    public override void DamageCustomEffects()
    {
        SetBloodEffect();
    }

    // Heal feedback is handled by PlayerFeedbacksController.PlayHeal()
    // which is called from PlayerLifeController.HandleHeal via OnHeal.
    public override void Heal(float amount)
    {
        base.Heal(amount);
        if (_currentHealth >= (MaxHealth * 0.35f))
            bloodImages[2].SetActive(false);
    }

    protected override void Die()
    {
        SetFadeOutBloodHard();
        base.Die();
    }

    public void SetBloodEffect()
    {
        if (MaxHealth <= 0f) return;

        int idx;
        if (_currentHealth <= (MaxHealth * 0.35f) && _currentHealth > 0f)
            idx = 2;
        else if (_currentHealth <= (MaxHealth * 0.85f) && _currentHealth > (MaxHealth * 0.25f))
            idx = 1;
        else
            idx = 0;

        SetActiveBloodIndex(idx);
    }

    public void SetFadeOutBloodHard()
    {
        if (bloodImages[2].activeSelf)
            fadeOutLowHp.StartEffect();
    }

    private void SetActiveBloodIndex(int idx)
    {
        for (int i = 0; i < bloodImages.Length; i++)
        {
            if (bloodImages[i] != null)
                bloodImages[i].SetActive(i == idx);
        }
    }

    #endregion
}
