using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "FeintStep", menuName = "Enemies/Movement Action/Feint Step", order = 0)]
public class FeintStepAction : EnemyAction
{
    [Header("Feint Settings")]
    public float stepInDistance = 0.75f;
    public float stepOutDistance = 1.10f;

    public float totalDuration = 0.55f;
    [Range(0.1f, 0.9f)] public float phaseSplit01 = 0.45f;

    public float moveSpeed = 1.5f;

    [Header("Pathing")]
    public float sampleRadius = 1.4f;

    private sealed class FeintRuntime
    {
        public float t0;
        public bool steppedOut;
        public Vector3 destIn;
        public Vector3 destOut;
        public bool hasDests;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (!ctx.hasLOS) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        // Prefer when attack intent is building but not max
        float w = Mathf.Clamp01(1f - Mathf.Abs(ctx.attackIntent01 - 0.65f) * 1.8f);
        score *= Mathf.Lerp(0.75f, 1.25f, w);

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

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<FeintRuntime>(this);
        rt.t0 = Time.time;
        rt.steppedOut = false;

        Vector3 toTarget = target.position - ctx.Owner.transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            toTarget = ctx.Owner.transform.forward;

        toTarget.Normalize();

        // Step in
        Vector3 destIn = ctx.Owner.transform.position + toTarget * stepInDistance;
        if (NavMesh.SamplePosition(destIn, out var hitIn, sampleRadius, NavMesh.AllAreas))
        {
            destIn = hitIn.position;
        }

        // Step out
        Vector3 destOut = ctx.Owner.transform.position - toTarget * stepOutDistance;
        if (NavMesh.SamplePosition(destOut, out var hitOut, sampleRadius, NavMesh.AllAreas))
        {
            destOut = hitOut.position;
        }

        rt.destIn = destIn;
        rt.destOut = destOut;
        rt.hasDests = true;

        ctx.Movement.PushSpeedOverride(moveSpeed, totalDuration + 0.1f);

        ctx.Agent.isStopped = false;
        ctx.Agent.updatePosition = true;
        ctx.Agent.updateRotation = true;
        ctx.Agent.SetDestination(destIn);

        ctx.CombatPlanner.ScheduleRelease(totalDuration);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<FeintRuntime>(this);
        if (!rt.hasDests) return;

        float elapsed = Time.time - rt.t0;
        float split = totalDuration * Mathf.Clamp01(phaseSplit01);

        if (!rt.steppedOut && elapsed >= split)
        {
            rt.steppedOut = true;
            ctx.Agent.SetDestination(rt.destOut);
        }
    }
}
