using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "Flank", menuName = "Enemies/Movement Action/Flank", order = 0)]
public class FlankAction : EnemyAction
{
    [Header("Flank Settings")] public float radius = 2.6f;
    public float angleOffset = 70f;
    public float angularSpeed = 2.5f;
    public float duration = 0.85f;

    [Header("Hard Filters")] public bool requireLOS = true;
    public bool requireSpaceFree = true;
    public float minDistance = 1.2f;
    public float maxDistance = 6.0f;

    private float side;

    public override float Evaluate(ScoreContext ctx)
    {
        if (requireLOS && !ctx.hasLOS) return -Mathf.Infinity;
        if (requireSpaceFree && !ctx.spaceFree) return -Mathf.Infinity;
        if (minDistance > 0f && ctx.distance < minDistance) return -Mathf.Infinity;
        if (maxDistance > 0f && ctx.distance > maxDistance) return -Mathf.Infinity;

        float score = base.Evaluate(ctx);
        return score <= 0f ? -Mathf.Infinity : score;
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

        Vector3 toEnemy = ctx.Owner.transform.position - target.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = -target.forward;

        float cross = Vector3.Cross(target.forward, toEnemy.normalized).y;
        side = cross > 0 ? 1f : -1f;
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

        // Use AlertLevel instead of HasVisual so the flank aborts correctly
        // when the enemy loses confirmed combat vision.
        bool hasConfirmedTarget = ctx.Perception.AlertLevel == Enums.AlertLevel.Combat;

        if (dist < minDistance || dist > maxDistance || requireLOS && !hasConfirmedTarget)
        {
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        Vector3 pivot = target.position;
        Vector3 toEnemy = ctx.Owner.transform.position - pivot;
        toEnemy.y = 0f;

        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = -target.forward;

        float currentAngle = Mathf.Atan2(toEnemy.z, toEnemy.x);
        float targetAngle = currentAngle + side * angleOffset * Mathf.Deg2Rad;
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, angularSpeed * dt);

        Vector3 dest = pivot + new Vector3(Mathf.Cos(currentAngle), 0f, Mathf.Sin(currentAngle)) * radius;

        if (NavMesh.SamplePosition(dest, out var hit, 1.3f, NavMesh.AllAreas))
        {
            dest = hit.position;
        }

        ctx.Agent.isStopped = false;
        ctx.Agent.updateRotation = true;
        ctx.Agent.updatePosition = true;
        ctx.Agent.SetDestination(dest);
    }
}