using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "BackOff", menuName = "Enemies/Movement Action/Back Off", order = 0)]
public class BackOffAction : EnemyAction
{
    [Header("BackOff Settings")] [Tooltip("Target distance we want to maintain from the player after completing the back-off.")]
    public float targetRange = 3.2f;

    [Tooltip("Duration of the backward movement before releasing the action.")]
    public float duration = 0.5f;

    [Tooltip("Movement speed during back-off.")]
    public float backOffSpeed = 1.6f;

    [Tooltip("Adds slight variety to range (prevents robotic repetition).")]
    public float rangeJitter = 0.25f;

    [Tooltip("Adds slight variety to duration (prevents robotic repetition).")]
    public float durationJitter01 = 0.15f;

    [Header("Hard Filters")] [Tooltip("Maximum distance beyond which backing off no longer makes sense (0 = no limit).")]
    public float maxEffectiveDistance = 6.0f;

    [Tooltip("Does this action require line of sight with the target to back off?")]
    public bool requireLOS = false;

    [Header("Performance")] public float repathInterval = 0.15f;
    public float minTargetMove = 0.20f;
    public float minDestDelta = 0.10f;

    private sealed class BackOffRuntime
    {
        public float nextRepathTime;
        public Vector3 lastTargetPos;
        public Vector3 lastDest;
        public bool hasLast;

        public float targetRangeRuntime;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (maxEffectiveDistance > 0f && ctx.distance > maxEffectiveDistance) return -Mathf.Infinity;

        // No point backing off if already at or beyond the target range.
        // Prevents the rapid execute→immediate-release loop that reads as "action none".
        if (ctx.distance >= targetRange) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);

        if (requireLOS && !ctx.hasLOS) score *= 0.25f;

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        // IMPORTANT: Ensure speed is controlled by the movement system (agent.speed is overwritten every frame)
        float dur = duration * Random.Range(1f - durationJitter01, 1f + durationJitter01);

        ctx.Movement.PushSpeedOverride(backOffSpeed, dur + 0.1f);

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<BackOffRuntime>(this);
        rt.nextRepathTime = 0f;
        rt.hasLast = false;
        rt.targetRangeRuntime = Mathf.Max(0.5f, targetRange + Random.Range(-rangeJitter, rangeJitter));

        UpdateDestination(ctx, rt, force: true);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<BackOffRuntime>(this);

        float stopDist = rt.targetRangeRuntime * 0.95f;
        float stopDist2 = stopDist * stopDist;

        if (ctx.Movement.DistanceToTargetSqr() >= stopDist2)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        UpdateDestination(ctx, rt, force: false);
    }

    private void UpdateDestination(EnemyContext ctx, BackOffRuntime rt, bool force)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        var now = Time.time;
        var targetPos = target.position;

        float minMove2 = minTargetMove * minTargetMove;

        if (!force && rt.hasLast)
        {
            bool targetMovedEnough = (targetPos - rt.lastTargetPos).sqrMagnitude >= minMove2;
            if (now < rt.nextRepathTime && !targetMovedEnough) return;
        }

        rt.nextRepathTime = now + repathInterval;
        rt.lastTargetPos = targetPos;

        var awayDir = (ctx.Owner.transform.position - targetPos);
        awayDir.y = 0f;

        if (awayDir.sqrMagnitude < 0.001f)
            awayDir = -ctx.Owner.transform.forward;

        awayDir.Normalize();

        var retreatMultiplier = ctx.Health.CurrentHealth01 < 0.3f ? 1.3f : 1f;

        var dest = ctx.Owner.transform.position + awayDir * (rt.targetRangeRuntime * retreatMultiplier);

        if (NavMesh.SamplePosition(dest, out var hit, 2.0f, NavMesh.AllAreas))
            dest = hit.position;

        float minDest2 = minDestDelta * minDestDelta;
        if (rt.hasLast && (dest - rt.lastDest).sqrMagnitude < minDest2) return;

        rt.lastDest = dest;
        rt.hasLast = true;

        ctx.Movement.SafeSetDestination(ctx.Movement.ComputeSeparatedDestination(dest));
    }
}