using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "AggressivePressure", menuName = "Enemies/Movement Action/Aggressive Pressure V2", order = 0)]
public class AggressivePressureAction : EnemyAction
{
    [Header("Pressure Settings")] [Tooltip("Sweet spot distance - just outside attack range to threaten")]
    public float optimalPressureDistance = 2.5f;

    [Tooltip("Don't pressure if too close (let attack happen)")]
    public float minPressureDistance = 1.5f;

    [Tooltip("Don't pressure if too far (close gap instead)")]
    public float maxPressureDistance = 4.5f;

    [Tooltip("How long to maintain pressure")]
    public float duration = 1.5f;

    [Tooltip("Movement speed during pressure")]
    public float pressureSpeed = 2.2f;

    [Header("Behavior Tuning")] [Tooltip("How tightly to maintain the optimal distance (higher = more precise)")] [Range(0.1f, 2f)]
    public float distanceAggression = 0.8f;

    [Tooltip("Acceptable distance error before adjusting")]
    public float distanceTolerance = 0.4f;

    [Header("Hard Filters")] public bool requireLOS = true;
    public bool requireSpaceFree = false;

    [Header("Performance")] public float repathInterval = 0.15f;
    public float sampleRadius = 1.5f;

    private sealed class PressureRuntime
    {
        public float nextRepathTime;
        public Vector3 lastTargetPos;
        public float actualOptimalDistance;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        // Hard filters first
        if (ctx.distance < minPressureDistance) return -Mathf.Infinity;
        if (ctx.distance > maxPressureDistance) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        // CRITICAL: Reduce score significantly when we have a ready attack
        // This allows attacks to take priority when in range
        if (ctx.hasReadyAttack)
        {
            // If we're near optimal attack range, let the attack happen
            float distanceError = Mathf.Abs(ctx.distance - optimalPressureDistance);
            if (distanceError < distanceTolerance * 1.5f)
            {
                score *= 0.3f; // Very low score - attack should win
            }
            else
            {
                score *= 0.6f; // Moderate score - positioning might win
            }
        }

        if (requireLOS && !ctx.hasLOS) score *= 0.2f;
        if (requireSpaceFree && !ctx.spaceFree) score *= 0.4f;

        // Favor pressure when at medium range
        float distanceScore = 1f - Mathf.Abs(ctx.distance - optimalPressureDistance) / maxPressureDistance;
        score *= Mathf.Clamp01(distanceScore);

        // Increase score with attack intent (but not too much)
        score *= Mathf.Lerp(0.8f, 1.1f, ctx.attackIntent01);

        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<PressureRuntime>(this);

        rt.nextRepathTime = 0f;
        rt.lastTargetPos = Vector3.positiveInfinity;

        // Add slight randomness to avoid robotic behavior
        rt.actualOptimalDistance = optimalPressureDistance + Random.Range(-0.2f, 0.2f);

        // Set movement speed
        ctx.Movement.PushSpeedOverride(pressureSpeed, duration + 0.2f);

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

        // Check if we should release to allow attack
        float currentDistance = Vector3.Distance(ctx.Owner.transform.position, target.position);

        // If we have a ready attack and we're in good range, release pressure
        var sc = new ScoreContext { distance = currentDistance };
        if (ctx.CombatPlanner.Debug_GetLastContext().hasReadyAttack)
        {
            float distanceError = Mathf.Abs(currentDistance - optimalPressureDistance);
            if (distanceError < distanceTolerance)
            {
                // We're in good position, release so attack can happen
                ctx.CombatPlanner.ForceRelease();
                return;
            }
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<PressureRuntime>(this);
        UpdateDestination(ctx, rt, force: false);
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
        ctx.Movement.ClearSpeedOverride();
    }

    private void UpdateDestination(EnemyContext ctx, PressureRuntime rt, bool force)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        float now = Time.time;
        if (!force && now < rt.nextRepathTime) return;

        rt.nextRepathTime = now + repathInterval;

        Vector3 enemyPos = ctx.Owner.transform.position;
        Vector3 targetPos = target.position;

        // Calculate direction to target
        Vector3 toTarget = targetPos - enemyPos;
        toTarget.y = 0f;

        float currentDistance = toTarget.magnitude;

        if (currentDistance < 0.001f)
        {
            return; // Too close, don't move
        }

        Vector3 dirToTarget = toTarget / currentDistance;

        // Calculate desired position at optimal distance
        Vector3 desiredPosition = targetPos - dirToTarget * rt.actualOptimalDistance;

        // Sample NavMesh
        Vector3 dest = desiredPosition;
        if (NavMesh.SamplePosition(desiredPosition, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            dest = hit.position;
        }

        // Only move if we're not already close enough
        float distanceToDestination = Vector3.Distance(enemyPos, dest);
        if (!force && distanceToDestination < distanceTolerance)
        {
            // Already at good position
            return;
        }

        rt.lastTargetPos = targetPos;

        ctx.Movement.SafeSetDestination(ctx.Movement.ComputeSeparatedDestination(dest));
    }
}