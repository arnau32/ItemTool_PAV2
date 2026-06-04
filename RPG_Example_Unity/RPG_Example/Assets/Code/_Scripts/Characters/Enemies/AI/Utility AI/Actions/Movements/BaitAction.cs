using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "Bait", menuName = "Enemies/Movement Action/Bait", order = 0)]
public class BaitAction : EnemyAction
{
    [Header("Bait Settings")]
    public float baitDistance = 1.5f;

    [Tooltip("Movement speed during bait.")]
    public float baitSpeed = 1.35f;

    public float duration = 0.45f;

    [Header("Hard Filters")]
    public bool requireLOS = true;
    public float maxEffectiveDistance = 4.5f;

    [Header("Performance")]
    public float repathInterval = 0.12f;
    public float minDestDelta = 0.10f;

    private sealed class BaitRuntime
    {
        public float nextRepathTime;
        public Vector3 lastDest;
        public bool hasLast;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (requireLOS && !ctx.hasLOS) return -Mathf.Infinity;
        if (maxEffectiveDistance > 0 && ctx.distance > maxEffectiveDistance) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        ctx.CombatPlanner.ScheduleRelease(duration);

        var target = ctx.Perception.CurrentTarget;
        if (target == null)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        ctx.Movement.PushSpeedOverride(baitSpeed, duration + 0.1f);

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<BaitRuntime>(this);
        rt.nextRepathTime = 0f;
        rt.hasLast = false;

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

        float dist = Vector3.Distance(ctx.Owner.transform.position, target.position);
        if (dist > maxEffectiveDistance)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<BaitRuntime>(this);
        UpdateDestination(ctx, rt, force: false);
    }

    private void UpdateDestination(EnemyContext ctx, BaitRuntime rt, bool force)
    {
        float now = Time.time;
        if (!force && now < rt.nextRepathTime) return;
        rt.nextRepathTime = now + repathInterval;

        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        Vector3 retreatDir = (ctx.Owner.transform.position - target.position);
        retreatDir.y = 0f;

        if (retreatDir.sqrMagnitude < 0.01f)
            retreatDir = -ctx.Owner.transform.forward;

        retreatDir.Normalize();

        Vector3 dest = ctx.Owner.transform.position + retreatDir * baitDistance;

        if (NavMesh.SamplePosition(dest, out var hit, 1.2f, NavMesh.AllAreas))
            dest = hit.position;

        if (rt.hasLast && (dest - rt.lastDest).sqrMagnitude < (minDestDelta * minDestDelta))
            return;

        rt.lastDest = dest;
        rt.hasLast = true;

        ctx.Agent.isStopped = false;
        ctx.Agent.updateRotation = true;
        ctx.Agent.updatePosition = true;
        ctx.Agent.SetDestination(dest);
    }
}
