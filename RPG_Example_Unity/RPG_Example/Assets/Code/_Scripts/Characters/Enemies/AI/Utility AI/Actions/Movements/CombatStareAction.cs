using UnityEngine;

[CreateAssetMenu(fileName = "CombatStare", menuName = "Enemies/Movement Action/Combat Stare", order = 0)]
public class CombatStareAction : EnemyAction
{
    [Header("Stare Settings")]
    public float minDuration = 0.18f;
    public float maxDuration = 0.55f;

    [Tooltip("If true, stops movement smoothly (keeps animation damping clean).")]
    public bool smoothStop = true;

    [Header("Hard Filters")]
    [Tooltip("Don't stare if the player is further than this (looks stupid at range).")]
    public float maxStareDistance = 5.0f;

    [Tooltip("Don't stare if the player is closer than this (should react, not stare).")]
    public float minStareDistance = 1.2f;

    [Header("Reactive Bail")]
    [Tooltip("If the player gets closer than this during the stare, abort early.")]
    public float bailDistance = 1.8f;

    [Tooltip("If the player is winding up an attack, abort to allow a reaction.")]
    public bool bailOnPlayerWindup = true;

    [Tooltip("Player windup threshold (0-1) above which we bail.")]
    [Range(0f, 1f)] public float windupBailThreshold = 0.15f;

    private sealed class StareRuntime
    {
        public float endTime;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (ctx.distance > maxStareDistance) return -Mathf.Infinity;
        if (ctx.distance < minStareDistance) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        float mid = 1f - Mathf.Abs(ctx.attackIntent01 - 0.5f) * 2f; // peak at 0.5
        score *= Mathf.Lerp(0.85f, 1.15f, mid);

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        float dur = Random.Range(minDuration, maxDuration);

        ctx.Agent.ResetPath();
        ctx.Agent.isStopped = true;

        if (smoothStop) ctx.Movement.SmoothStop();
        else ctx.Movement.HardStop();

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<StareRuntime>(this);
        rt.endTime = Time.time + dur;

        ctx.CombatPlanner.ScheduleRelease(dur);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        // Check if stare time expired
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<StareRuntime>(this);
        if (Time.time >= rt.endTime)
        {
            ReleaseStare(ctx);
            return;
        }

        var target = ctx.Perception.CurrentTarget;
        if (target == null)
        {
            ReleaseStare(ctx);
            return;
        }

        // Bail if player gets too close — enemy should react, not stand still
        float dist = Vector3.Distance(ctx.Owner.transform.position, target.position);
        if (dist < bailDistance)
        {
            ReleaseStare(ctx);
            return;
        }

        // Bail if player is winding up an attack (we have opportunity window data)
        if (bailOnPlayerWindup)
        {
            var targetable = ctx.Perception.CurrentTargetableTarget;
            if (targetable != null && targetable.TargetWindUp01 > windupBailThreshold)
            {
                ReleaseStare(ctx);
                return;
            }
        }
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
        ctx.Agent.isStopped = false;
    }

    private void ReleaseStare(EnemyContext ctx)
    {
        // Clean up agent state before releasing so the next action starts clean
        ctx.Agent.isStopped = false;
        ctx.CombatPlanner.ForceRelease();
    }
}