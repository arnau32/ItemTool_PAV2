using UnityEngine;

[CreateAssetMenu(fileName = "Heal", menuName = "Enemies/Utility/Heal", order = 0)]
public class HealAction : EnemyAction
{
    #region Fields

    [Header("Heal Settings")]
    [Tooltip("HP restored immediately when the heal animation starts.")]
    [Min(0f)] public float healAmount = 30f;

    [Tooltip("Animation cross-fade duration in seconds.")]
    [Min(0f)] public float crossFade = 0.15f;

    [Tooltip("Duration in seconds the planner is locked during the heal animation.")]
    [Min(0f)] public float animationDuration = 1.5f;

    #endregion

    #region Public API

    public override float Evaluate(ScoreContext ctx)
    {
        float score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;
        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashHeal, crossFade, EnemyAnimHashes.LayerOverride);
        ctx.Health.Heal(healAmount);
        ctx.CombatPlanner.ScheduleRelease(animationDuration);
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
    }

    #endregion
}