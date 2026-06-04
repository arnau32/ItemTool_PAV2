using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "EnemyDodgeAction", menuName = "Enemies/Utility/Dodge", order = 0)]
public class DodgeAction : EnemyAction
{
    #region Fields

    [Header("Dodge Settings")]
    [Tooltip("Animation cross-fade duration in seconds.")]
    [Min(0f)] public float crossFade = 0.1f;

    [Tooltip("Duration in seconds the dodge lasts before the enemy returns to normal.")]
    [Min(0f)] public float animationDuration = 0.6f;

    [Tooltip("If true, picks left or right randomly. If false, always dodges to the right of the player direction.")]
    public bool randomizeSide = true;

    [Tooltip("Seconds before the enemy can dodge again after finishing a dodge.")]
    [Min(0f)] public float minCooldown = 4f;
    [Tooltip("Seconds before the enemy can dodge again after finishing a dodge.")]
    [Min(0f)] public float maxCooldown = 8f;
    
    [Header("NavMesh Validation")]
    [Tooltip("Estimated horizontal displacement of the dodge animation. Tune to match the root motion distance of your dodge clip.")]
    [Min(0f)] public float dodgeDistance = 1.5f;

    [Tooltip("NavMesh sample radius for the pre-validation check. Increase slightly if terrain is uneven.")]
    [Min(0f)] public float navSampleRadius = 0.6f;

    #endregion

    #region Runtime State

    private sealed class DodgeRuntime
    {
        public float elapsed;
        public bool finished;
        public bool aborted;
    }

    #endregion

    #region Public API

    public override float Evaluate(ScoreContext ctx)
    {
        float score = base.Evaluate(ctx);
        if (score <= 0f) return -Mathf.Infinity;
        return score;
    }

    public override void Execute(EnemyContext ctx)
    {
        Transform owner = ctx.Owner.transform;
        Transform target = ctx.Perception.CurrentTarget;

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<DodgeRuntime>(this);
        rt.elapsed  = 0f;
        rt.finished = false;
        rt.aborted  = false;

        if (target != null)
        {
            Vector3 toPlayer = target.position - owner.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                toPlayer.Normalize();

                bool preferLeft  = randomizeSide ? Random.value > 0.5f : false;
                Vector3 leftDir  = Vector3.Cross(toPlayer, Vector3.up);
                Vector3 rightDir = Vector3.Cross(Vector3.up, toPlayer);

                Vector3 preferred = preferLeft ? leftDir : rightDir;
                Vector3 fallback  = preferLeft ? rightDir : leftDir;

                Vector3 dodgeDir;

                if (IsNavMeshValid(owner.position, preferred))
                {
                    dodgeDir = preferred;
                }
                else if (IsNavMeshValid(owner.position, fallback))
                {
                    dodgeDir = fallback;
                }
                else
                {
                    // Neither side has NavMesh coverage — skip this dodge.
                    rt.aborted = true;
                    return;
                }

                owner.rotation = Quaternion.LookRotation(dodgeDir);
            }
        }

        ctx.Movement.SetRootMotion(true);
        // Lock facing so CombatWanderState does not rotate the enemy mid-dodge.
        ctx.Movement.LockFacing(true);
        ctx.Health.SetInvulnerable(true);

        ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashDodge, crossFade, EnemyAnimHashes.LayerOverride);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<DodgeRuntime>(this);
        if (rt.finished) return;

        if (rt.aborted)
        {
            rt.finished = true;
            ctx.CombatPlanner.RegisterCooldown(this, GetRandomCooldown());
            ctx.CombatPlanner.ClearRuntime(this);
            ctx.CombatPlanner.ForceRelease();
            return;
        }

        rt.elapsed += dt;
        if (rt.elapsed < animationDuration) return;

        // Animation finished — hard-snap toward target before releasing the facing
        // lock. Without this, the enemy exits the dodge still oriented in the dodge
        // direction, the next perception tick may miss the player (wrong FOV), and
        // HasVisual drops mid-combat.
        Transform target = ctx.Perception.CurrentTarget;
        if (target != null)
        {
            Vector3 toTarget = target.position - ctx.Owner.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                ctx.Owner.transform.rotation = Quaternion.LookRotation(toTarget.normalized);
        }

        rt.finished = true;
        ctx.Movement.SetRootMotion(false);
        ctx.Health.SetInvulnerable(false);
        ctx.CombatPlanner.RegisterCooldown(this, GetRandomCooldown());
        ctx.CombatPlanner.ClearRuntime(this);
        ctx.CombatPlanner.ForceRelease();
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<DodgeRuntime>(this);
        if (!rt.finished && !rt.aborted)
        {
            ctx.Movement.SetRootMotion(false);
            ctx.Health.SetInvulnerable(false);
        }

        ctx.CombatPlanner.ClearRuntime(this);
        base.OnInterrupted(ctx);
    }

    #endregion

    #region Helpers

    private bool IsNavMeshValid(Vector3 origin, Vector3 dir)
    {
        // If origin itself is off-mesh (e.g. prior root motion drifted the enemy),
        // NavMesh.Raycast returns true immediately (boundary at origin) and both
        // sides always fail, causing a silent abort on every dodge attempt.
        if (!NavMesh.SamplePosition(origin, out _, 0.3f, NavMesh.AllAreas))
            return false;

        Vector3 dest = origin + dir * dodgeDistance;

        // Tight radius: ensures the sampled point is truly on the mesh near the
        // intended endpoint, not just the closest edge up to navSampleRadius away.
        if (!NavMesh.SamplePosition(dest, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            return false;
        if ((hit.position - dest).sqrMagnitude > 0.09f) // 0.3 m tolerance
            return false;

        // NavMesh.Raycast returns true if the surface ray hits a NavMesh boundary.
        return !NavMesh.Raycast(origin, dest, out _, NavMesh.AllAreas);
    }
    
    private float GetRandomCooldown() => Random.Range(minCooldown, maxCooldown);

    #endregion
}
