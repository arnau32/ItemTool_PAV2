using UnityEngine;

public abstract class AttackAction : EnemyAction
{
    public AttackData attackData;

    [Header("Interrupt Resistance")]
    [Tooltip("If false, Knockback hits do not interrupt this attack.")]
    public bool canBeInterruptedByKnockback = true;
    [Tooltip("If false, Knockdown hits do not interrupt this attack.")]
    public bool canBeInterruptedByKnockdown = true;

    [Header("Timing")]
    [Min(0f)] public float minCooldown = 0.8f;
    [Min(0f)] public float maxCooldown = 1.2f;
    public float commitSeconds = 0.35f;
    public float cadenceDelay = 0.9f;

    [Header("Extra Utility Config")] public float selectionWeight = 1f;
    public float randomness = 0f;
    public float repeatPenalty = 0f;

    [Header("Positioning")] [Tooltip("Optional override for positioning priority. <= 0 means auto-detect from considerations.")]
    public float positioningOptimalRangeOverride = 0f;

    public virtual bool OnHitFinished(EnemyContext ctx) => false;
}