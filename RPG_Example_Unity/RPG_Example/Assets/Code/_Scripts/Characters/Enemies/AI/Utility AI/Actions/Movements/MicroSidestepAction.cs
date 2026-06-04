using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "MicroSidestep", menuName = "Enemies/Movement Action/Micro Sidestep", order = 0)]
public class MicroSidestepAction : EnemyAction
{
    [Header("Sidestep Settings")]
    public float stepDistance = 0.9f;
    public float forwardBias = 0.15f;

    public float duration = 0.35f;
    public float moveSpeed = 1.4f;

    [Header("Side Selection")]
    [Range(0f, 1f)] public float flipChance = 0.35f;
    public float minHoldBeforeFlip = 0.6f;

    [Header("Pathing")]
    public float sampleRadius = 1.2f;

    private sealed class MicroSidestepRuntime
    {
        public int dirTarget = 1;
        public float lastFlipTime;
        public Vector3 dest;
        public bool hasDest;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        // Avoid sidestep when too far (it should be a close-range "dance" tool)
        if (ctx.distance > 5.5f) score *= 0.35f;

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<MicroSidestepRuntime>(this);

        // Decide side with some memory
        if (Time.time - rt.lastFlipTime >= minHoldBeforeFlip && Random.value < flipChance)
        {
            rt.dirTarget *= -1;
            rt.lastFlipTime = Time.time;
        }

        var right = ctx.Owner.transform.right * rt.dirTarget;
        var fwd = ctx.Owner.transform.forward * forwardBias;

        var dest = ctx.Owner.transform.position + (right + fwd).normalized * stepDistance;

        if (NavMesh.SamplePosition(dest, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            dest = hit.position;
        }

        rt.dest = dest;
        rt.hasDest = true;

        ctx.Movement.PushSpeedOverride(moveSpeed, duration + 0.1f);

        ctx.Agent.isStopped = false;
        ctx.Agent.updatePosition = true;
        ctx.Agent.updateRotation = true;
        ctx.Agent.SetDestination(dest);

        ctx.CombatPlanner.ScheduleRelease(duration);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        if (ctx.Agent.pathPending) return;
        if (ctx.Agent.remainingDistance <= 0.12f)
        {
            ctx.CombatPlanner.ForceRelease();
        }
    }
}
