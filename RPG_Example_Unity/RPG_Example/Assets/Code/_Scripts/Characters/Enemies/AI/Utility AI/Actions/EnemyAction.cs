using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAction : ScriptableObject
{
    [Header("Utility AI")]
    public List<UtilityConsideration> considerations;
    public Enums.ActionCategory category;
    public bool isMaintenanceMovement;

    [Header("Base Action Settings")]
    public bool  canBeInterrupted = true;
    [Tooltip("Stay looking at the player while doing the action?")]
    public bool  useHardLook = true;
    public float baseScore = 100f;

    public virtual float Evaluate(ScoreContext ctx)
    {
        int count = considerations != null ? considerations.Count : 0;
        if (count == 0) return baseScore;

        float product    = 1f;
        int   validCount = 0;

        for (int i = 0; i < count; i++)
        {
            var c = considerations[i];
            if (c == null) continue;

            product *= Mathf.Clamp01(c.Evaluate(ctx));
            validCount++;
        }

        if (validCount == 0) return baseScore;

        float utility01 = Mathf.Pow(product, 1f / validCount);
        return baseScore * utility01;
    }

    public abstract void Execute(EnemyContext ctx);

    public virtual void Tick(EnemyContext ctx, float dt) { }

    public virtual void OnInterrupted(EnemyContext ctx)
    {
        ctx.Agent.ResetPath();
        ctx.Agent.updatePosition = true;
    }
}