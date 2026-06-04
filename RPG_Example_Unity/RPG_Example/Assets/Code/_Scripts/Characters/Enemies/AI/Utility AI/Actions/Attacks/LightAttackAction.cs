using UnityEngine;

// MUST BE fast and short ranged
[CreateAssetMenu(fileName = "LightAttack", menuName = "Enemies/AttackAction/Light Attack", order = 0)]
public class LightAttackAction : AttackAction
{
    [Header("Light Attack Filters")]
    public float hardMaxDistance = 4.0f;
    public bool requireLOS = true;

    public override float Evaluate(ScoreContext ctx)
    {
        if (requireLOS && !ctx.hasLOS) return -Mathf.Infinity;
        if (hardMaxDistance > 0f && ctx.distance > hardMaxDistance) return -Mathf.Infinity;

        float score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        ctx.CombatPlanner.BeginAction(category, commitSeconds, cadenceDelay);

        var target = ctx.Perception.CurrentTarget;
        if (target != null)
        {
            Vector3 dir = target.position - ctx.Owner.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                ctx.Owner.transform.rotation = Quaternion.LookRotation(dir);
        }

        if (!useHardLook && target != null)
        {
            Vector3 dir = target.position - ctx.Owner.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                ctx.Movement.ForcedAttackDirection = dir.normalized;
        }

        if (attackData != null)
        {
            attackData.Execute(ctx.Animation.PlayTargetAnimation);
        }
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        base.Tick(ctx, dt);
        // NO polling aquí. El planner ya decide fin del ataque y mantiene ventanas.
    }
}