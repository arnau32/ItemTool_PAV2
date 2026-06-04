using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "OrbitStrafe", menuName = "Enemies/Movement Action/Orbit Strafe", order = 0)]
public class OrbitStrafeAction : EnemyAction
{
    [Header("Orbit")] public float desiredRadius = 3.0f;
    public float duration = 0.9f;

    [Tooltip("-1 = left | 1 = right | 0 = auto")] [Range(-1, 1)]
    public int direction = 0;

    [Header("Motion")] public float strafeSpeed = 1.6f;

    [Tooltip("How fast we correct radius errors (bigger = snappier).")]
    public float radialCorrection = 3.5f;

    [Tooltip("Angular speed in degrees/sec around the target (peak).")]
    public float angularSpeedDeg = 140f;

    [Header("Life / Variety")] [Range(0f, 1f)]
    public float flipChanceOnExecute = 0.22f;

    public float minHoldBeforeFlip = 0.65f;
    public float radiusJitter = 0.35f;
    public float durationJitter01 = 0.20f;

    [Header("Smooth Direction Change")] [Tooltip("Seconds to blend direction when flipping side (prevents animation snapping).")]
    public float flipBlendTime = 0.28f;

    [Tooltip("Optional: damp angular acceleration for more weight.")]
    public float angularAccelDamp = 10f;

    [Header("Periodic Flip")] [Tooltip("Minimum seconds between autonomous direction flips. 0 = disabled.")]
    public float minPeriodicFlipInterval = 2.0f;

    [Tooltip("Maximum seconds between autonomous direction flips.")]
    public float maxPeriodicFlipInterval = 4.0f;

    [Tooltip("Probability of actually flipping on each periodic check.")]
    [Range(0f, 1f)]
    public float periodicFlipChance = 0.65f;

    [Header("Hard Filters")] public bool requireLOS = true;
    public bool requireSpaceFree = false;
    public float minDistance = 1.5f;
    public float maxDistance = 6.0f;

    [Header("Performance")] public float repathInterval = 0.10f;

    [Header("Anti-jitter")] public float sampleRadius = 2.0f;

    [Tooltip("If the agent slows down too much, request a side change / repath.")]
    public float stuckSpeed = 0.10f;

    private sealed class OrbitStrafeRuntime
    {
        public int dirTarget;

        public float dir;
        public float dirVel; // SmoothDamp velocity

        public float nextRepathTime;

        public bool hasPrev;
        public bool prevAutoBraking;
        public float prevStoppingDistance;

        public float radius;
        public float angleRad;
        public bool hasAngle;

        public float lastFlipTime;

        public float omegaCurrent;
        public float omegaVel; // SmoothDamp velocity

        public float lastTickTime;
        public float nextPeriodicFlipTime;
    }

    public override float Evaluate(ScoreContext ctx)
    {
        if (minDistance > 0f && ctx.distance < minDistance) return -Mathf.Infinity;
        if (maxDistance > 0f && ctx.distance > maxDistance) return -Mathf.Infinity;

        var score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;

        if (requireLOS && !ctx.hasLOS) score *= 0.25f;
        if (requireSpaceFree && !ctx.spaceFree) score *= 0.25f;

        score *= Mathf.Lerp(1.0f, 0.75f, ctx.attackIntent01);

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

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<OrbitStrafeRuntime>(this);

        if (!rt.hasPrev)
        {
            rt.prevAutoBraking = ctx.Agent.autoBraking;
            rt.prevStoppingDistance = ctx.Agent.stoppingDistance;
            rt.hasPrev = true;
        }

        // Prevent braking to zero on micro destinations
        ctx.Agent.autoBraking = false;
        ctx.Agent.stoppingDistance = 0.05f;

        // Radius with jitter to avoid robotic circles
        rt.radius = Mathf.Max(0.75f, desiredRadius + Random.Range(-radiusJitter, radiusJitter));

        // Decide initial direction (target side)
        rt.dirTarget = ResolveDirection(ctx, rt, target);

        // Initialize smoothed dir close to target to avoid first-frame snap
        if (Mathf.Abs(rt.dir) < 0.001f) rt.dir = rt.dirTarget;
        else rt.dir = Mathf.Sign(rt.dir) == rt.dirTarget ? rt.dir : rt.dirTarget;

        rt.dirVel = 0f;
        rt.nextRepathTime = 0f;

        // Reset angular smoothing state
        rt.omegaCurrent = 0f;
        rt.omegaVel = 0f;

        // Initialize tick time so the first Tick has a proper dt baseline
        rt.lastTickTime = Time.time;
        rt.nextPeriodicFlipTime = Time.time + Random.Range(minPeriodicFlipInterval, maxPeriodicFlipInterval);

        // Ensure action-level speed override is applied (your EnemyMovement overwrites agent.speed every frame)
        float dur = duration * Random.Range(1f - durationJitter01, 1f + durationJitter01);
        ctx.Movement.PushSpeedOverride(strafeSpeed, dur + 0.15f);

        // Initialize angle from current position
        Vector3 enemyPos = ctx.Owner.transform.position;
        Vector3 pivot = target.position;
        Vector3 radial = enemyPos - pivot;
        radial.y = 0f;

        if (radial.sqrMagnitude < 0.0001f)
        {
            radial = -ctx.Owner.transform.forward;
            radial.y = 0f;
        }

        rt.angleRad = Mathf.Atan2(radial.z, radial.x);
        rt.hasAngle = true;

        UpdateDestination(ctx, rt, force: true);
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

        Vector3 toEnemy = ctx.Owner.transform.position - target.position;
        toEnemy.y = 0f;
        float d2 = toEnemy.sqrMagnitude;

        float minD = minDistance * 0.7f;
        float maxD = maxDistance * 1.1f;

        if (d2 < (minD * minD) || d2 > (maxD * maxD))
        {
            RestoreAgent(ctx);
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<OrbitStrafeRuntime>(this);

        // If we are nearly stuck, request a flip (but motion will blend smoothly)
        if (!ctx.Agent.pathPending && ctx.Agent.velocity.magnitude < stuckSpeed)
        {
            TryRequestFlip(rt);
        }

        // Periodic autonomous direction change to prevent endless single-direction orbiting.
        float now = Time.time;
        if (minPeriodicFlipInterval > 0f && now >= rt.nextPeriodicFlipTime)
        {
            rt.nextPeriodicFlipTime = now + Random.Range(minPeriodicFlipInterval, maxPeriodicFlipInterval);
            if (Random.value < periodicFlipChance)
                TryRequestFlip(rt);
        }

        // Smooth dir towards target (prevents instant side inversion)
        float smoothT = Mathf.Max(0.05f, flipBlendTime);
        rt.dir = Mathf.SmoothDamp(rt.dir, rt.dirTarget, ref rt.dirVel, smoothT);

        // Always advance the orbit angle every tick using real elapsed time,
        // even if we don't repath this frame. This keeps the angle in sync with
        // actual time progression and prevents drift.
        float tickDt = now - rt.lastTickTime;
        rt.lastTickTime = now;

        // Clamp to avoid huge jumps if there was a hitch
        tickDt = Mathf.Min(tickDt, 0.1f);

        AdvanceAngle(rt, tickDt);
        UpdateDestination(ctx, rt, force: false);
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
        RestoreAgent(ctx);
    }

    private void RestoreAgent(EnemyContext ctx)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<OrbitStrafeRuntime>(this);
        if (rt.hasPrev)
        {
            ctx.Agent.autoBraking = rt.prevAutoBraking;
            ctx.Agent.stoppingDistance = rt.prevStoppingDistance;
        }

        ctx.Movement.ClearSpeedOverride();
    }

    private void TryRequestFlip(OrbitStrafeRuntime rt)
    {
        if (Time.time - rt.lastFlipTime < minHoldBeforeFlip) return;

        rt.dirTarget *= -1;
        rt.lastFlipTime = Time.time;
    }

    private int ResolveDirection(EnemyContext ctx, OrbitStrafeRuntime rt, Transform target)
    {
        if (direction != 0) return direction > 0 ? 1 : -1;

        var ownerTransform = ctx.Owner.transform;
        var profile = ctx.BehaviorProfile;

        float leftFree01 = DirectionalSpaceCheck.GetSideFree01(ownerTransform, desiredRadius, 5);
        float rightFree01 = DirectionalSpaceCheck.GetRightFree01(ownerTransform, desiredRadius, 5);

        float leftPressure = DirectionalSpaceCheck.GetEnemyPressureLeft01(ownerTransform, desiredRadius, profile.enemyLayer);
        float rightPressure = DirectionalSpaceCheck.GetEnemyPressureRight01(ownerTransform, desiredRadius, profile.enemyLayer);

        bool canFlip = Time.time - rt.lastFlipTime >= minHoldBeforeFlip;

        if (canFlip && Random.value < flipChanceOnExecute && rt.dirTarget != 0)
        {
            rt.lastFlipTime = Time.time;
            return rt.dirTarget * -1;
        }

        if (leftFree01 < 0.15f && rightFree01 > leftFree01) return 1;
        if (rightFree01 < 0.15f && leftFree01 > rightFree01) return -1;

        float scoreLeft = leftFree01 - leftPressure * 0.6f + Random.Range(0f, 0.10f);
        float scoreRight = rightFree01 - rightPressure * 0.6f + Random.Range(0f, 0.10f);

        return scoreRight >= scoreLeft ? 1 : -1;
    }

    /// <summary>
    /// Advance the orbit angle using smoothed angular velocity.
    /// Called every tick (not just on repath) so the angle tracks real time.
    /// </summary>
    private void AdvanceAngle(OrbitStrafeRuntime rt, float dt)
    {
        // Compute desired omega and optionally damp acceleration for "weight"
        float omegaTarget = (angularSpeedDeg * Mathf.Deg2Rad) * rt.dir; // rt.dir is smoothed
        if (angularAccelDamp > 0f)
        {
            rt.omegaCurrent = Mathf.SmoothDamp(rt.omegaCurrent, omegaTarget, ref rt.omegaVel, 1f / angularAccelDamp);
        }
        else
        {
            rt.omegaCurrent = omegaTarget;
        }

        rt.angleRad += rt.omegaCurrent * dt;
    }

    private void UpdateDestination(EnemyContext ctx, OrbitStrafeRuntime rt, bool force)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        float now = Time.time;
        if (!force && now < rt.nextRepathTime) return;
        rt.nextRepathTime = now + repathInterval;

        Vector3 enemyPos = ctx.Owner.transform.position;
        Vector3 pivot = target.position;

        Vector3 radial = enemyPos - pivot;
        radial.y = 0f;

        float dist = radial.magnitude;
        if (dist < 0.001f)
        {
            radial = -ctx.Owner.transform.forward;
            radial.y = 0f;
            dist = radial.magnitude;
        }

        // Resync angle from actual position every repath to prevent drift.
        rt.angleRad = Mathf.Atan2(radial.z, radial.x);
        rt.hasAngle = true;

        float omegaTarget = (angularSpeedDeg * Mathf.Deg2Rad) * rt.dir;
        float stepAngle = omegaTarget * repathInterval;
        float destAngle = rt.angleRad + stepAngle;

        // Push destAngle away from other enemies orbiting the same pivot.
        destAngle = ApplyArcSeparation(ctx, pivot, rt.angleRad, destAngle);

        float radiusNow = Mathf.Lerp(dist, rt.radius, radialCorrection * repathInterval);

        float radialDrift = Random.Range(-0.25f, 0.25f);
        float orbitRadius = Mathf.Max(0.5f, radiusNow + radialDrift);

        Vector3 desiredOnCircle = new Vector3(Mathf.Cos(destAngle), 0f, Mathf.Sin(destAngle)) * orbitRadius;
        Vector3 dest = pivot + desiredOnCircle;

        bool found = false;
        Vector3 chosen = enemyPos;

        for (int i = 0; i < 3; i++)
        {
            if (NavMesh.SamplePosition(dest, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                chosen = hit.position;
                found = true;
                break;
            }

            // Slightly adjust angle if sampling fails
            float sign = (i % 2 == 0) ? 1f : -1f;
            float extra = (10f + i * 12f) * Mathf.Deg2Rad * sign * Mathf.Sign(rt.dirTarget);
            Vector3 alt = new Vector3(Mathf.Cos(rt.angleRad + extra), 0f, Mathf.Sin(rt.angleRad + extra)) * radiusNow;
            dest = pivot + alt;
        }

        if (!found)
        {
            // If we cannot find a good point, request a flip (motion will blend)
            TryRequestFlip(rt);
            return;
        }

        ctx.Movement.SafeSetDestination(chosen);
    }

    private static readonly Collider[] _arcBuffer = new Collider[8];

    /// Nudges destAngle away from other orbiting enemies within a close angular arc.
    private float ApplyArcSeparation(EnemyContext ctx, Vector3 pivot, float myAngle, float destAngle)
    {
        var profile = ctx.BehaviorProfile;
        if (profile == null || profile.enemyLayer == 0 || profile.separationRadius <= 0f)
            return destAngle;

        float searchRadius = profile.separationRadius * 1.5f;

        int count = Physics.OverlapSphereNonAlloc(
            ctx.Owner.transform.position, searchRadius, _arcBuffer,
            profile.enemyLayer, QueryTriggerInteraction.Collide);

        float angularPush = 0f;

        for (int i = 0; i < count; i++)
        {
            var col = _arcBuffer[i];
            if (col == null || col.transform == ctx.Owner.transform) continue;
            if (!col.CompareTag("Enemy")) continue;

            Vector3 toOther = col.transform.position - pivot;
            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.001f) continue;

            float otherAngle = Mathf.Atan2(toOther.z, toOther.x);
            float delta = Mathf.DeltaAngle(myAngle * Mathf.Rad2Deg, otherAngle * Mathf.Rad2Deg);

            // Only push when the other enemy is within ±50° arc.
            if (Mathf.Abs(delta) > 50f) continue;

            float dist = toOther.magnitude;
            float falloff = Mathf.Clamp01(1f - dist / searchRadius);

            // Push away: if other is at +delta, we push negative (and vice versa).
            angularPush -= Mathf.Sign(delta) * falloff * 35f * Mathf.Deg2Rad;
        }

        return destAngle + angularPush;
    }
}