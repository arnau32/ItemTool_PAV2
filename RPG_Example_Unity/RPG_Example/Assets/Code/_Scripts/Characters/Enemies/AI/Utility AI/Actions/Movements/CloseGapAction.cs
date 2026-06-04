using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "CloseGap", menuName = "Enemies/Movement Action/Close Gap", order = 0)]
public class CloseGapAction : EnemyAction
{
    [Header("CloseGap Settings")] public float preferred = 2.2f;
    public float duration = 0.6f;

    [Header("Hard Filters")] public float tolerance = 0.2f;
    public float maxEffectiveDistance = 12f;
    public bool requireLOS = true;

    [Header("Pathing")] [Tooltip("How far we try to project the target position onto navmesh.")]
    public float sampleRadius = 2.0f;

    [Header("Performance")] public float repathInterval = 0.15f;
    public float minTargetMove = 0.25f;

    [Header("Anti-stuck")] [Tooltip("If velocity is ~0 while far, force a repath after this time.")]
    public float stuckTime = 0.35f;

    private sealed class CloseGapRuntime
    {
        public float nextRepathTime;
        public Vector3 lastTargetPos;

        public float prevStoppingDistance;
        public bool prevAutoBraking;
        public bool hasPrev;

        public float stuckTimer;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (maxEffectiveDistance > 0f && ctx.distance > maxEffectiveDistance) return -Mathf.Infinity;

        if (ctx.distance <= preferred + tolerance) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        if (requireLOS && !ctx.hasLOS) score *= 0.25f;

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        ctx.Movement.SetRunningSpeed();

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<CloseGapRuntime>(this);

        if (!rt.hasPrev)
        {
            rt.prevStoppingDistance = ctx.Agent.stoppingDistance;
            rt.prevAutoBraking = ctx.Agent.autoBraking;
            rt.hasPrev = true;
        }

        ctx.Agent.autoBraking = true;
        ctx.Agent.stoppingDistance = Mathf.Max(0.05f, preferred);

        rt.nextRepathTime = 0f;
        rt.lastTargetPos = Vector3.positiveInfinity;
        rt.stuckTimer = 0f;

        RepathToTarget(ctx, rt, force: true);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null)
        {
            RestoreAgent(ctx);
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        Vector3 d = target.position - ctx.Owner.transform.position;
        d.y = 0f;
        float dist2 = d.sqrMagnitude;

        float stopDist = preferred + tolerance;
        float stopDist2 = stopDist * stopDist;

        if (dist2 <= stopDist2)
        {
            ctx.Agent.ResetPath();
            RestoreAgent(ctx);
            ctx.Movement.SnapToCombatWanderSpeed();
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<CloseGapRuntime>(this);

        float vel2 = ctx.Agent.velocity.sqrMagnitude;
        if (vel2 < 0.02f && dist2 > stopDist2 * 1.5f)
        {
            rt.stuckTimer += dt;
            if (rt.stuckTimer >= stuckTime)
            {
                rt.stuckTimer = 0f;
                rt.nextRepathTime = 0f;
                RepathToTarget(ctx, rt, force: true);
                return;
            }
        }
        else
        {
            rt.stuckTimer = 0f;
        }

        RepathToTarget(ctx, rt, force: false);
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
        RestoreAgent(ctx);
    }

    private void RepathToTarget(EnemyContext ctx, CloseGapRuntime rt, bool force)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        float now = Time.time;
        Vector3 targetPos = target.position;

        if (!force)
        {
            float minMove2 = minTargetMove * minTargetMove;
            bool movedEnough = (targetPos - rt.lastTargetPos).sqrMagnitude >= minMove2;
            if (now < rt.nextRepathTime && !movedEnough) return;
        }

        rt.nextRepathTime = now + repathInterval;
        rt.lastTargetPos = targetPos;

        Vector3 dest = targetPos;
        if (NavMesh.SamplePosition(targetPos, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            dest = hit.position;
        }

        ctx.Movement.SafeSetDestination(ctx.Movement.ComputeSeparatedDestination(dest));
    }

    private void RestoreAgent(EnemyContext ctx)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<CloseGapRuntime>(this);
        if (!rt.hasPrev) return;

        ctx.Agent.stoppingDistance = rt.prevStoppingDistance;
        ctx.Agent.autoBraking = rt.prevAutoBraking;
    }
}