using UnityEngine;

[CreateAssetMenu(fileName = "ComboAttack", menuName = "Enemies/AttackAction/Combo Attack", order = 0)]
public class ComboAttackAction : AttackAction
{
    [Header("Combo Hits")] [Tooltip("Additional hits after the first (attackData).")]
    public AttackData[] additionalHits;

    [Tooltip("Per-hit chain probability. Index 0 = chance to chain from Hit1 to Hit2, index 1 = Hit2 to Hit3, etc. " +
             "Leave an element at 0 to use the global comboChance fallback. Size should match additionalHits.")]
    [Range(0f, 1f)]
    public float[] chainChancePerHit;

    [Tooltip("Global fallback probability used when chainChancePerHit has no entry for that step. " +
             "1 = always chain, 0 = never chain.")]
    [Range(0f, 1f)]
    public float comboChance = 1f;

    [Tooltip("Seconds to wait between each hit when timeToChangeAnim is 0 on the current AttackData.")] [Min(0f)]
    public float gapBetweenHits = 0f;

    private sealed class ComboRuntime
    {
        public int hitIndex;
        public bool inGap;
        public float gapTimer;
        public bool chainedThisHit;
    }

    public override void Execute(EnemyContext ctx)
    {
        ctx.CombatPlanner.BeginAction(category, commitSeconds, cadenceDelay);
        FaceTarget(ctx);

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<ComboRuntime>(this);
        rt.hitIndex = -1;
        rt.inGap = false;
        rt.gapTimer = 0f;
        rt.chainedThisHit = false;

        if (attackData != null)
            attackData.Execute(ctx.Animation.PlayTargetAnimation);
    }

    public override void Tick(EnemyContext ctx, float dt)
    {
        var rt = ctx.CombatPlanner.GetOrCreateRuntime<ComboRuntime>(this);

        if (rt.inGap)
        {
            rt.gapTimer -= dt;
            if (rt.gapTimer > 0f) return;

            rt.inGap = false;
            LaunchHit(ctx, rt);
            return;
        }

        if (rt.chainedThisHit) return;
        if (!ctx.CombatPlanner.IsAttackRuntimeActive) return;
        if (additionalHits == null) return;

        int nextIndex = rt.hitIndex + 1;
        if (nextIndex >= additionalHits.Length) return;
        if (additionalHits[nextIndex] == null) return;

        bool hasHitAfterNext = nextIndex + 1 < additionalHits.Length
                               && additionalHits[nextIndex + 1] != null;
        if (!hasHitAfterNext) return;

        AttackData currentHitData = rt.hitIndex < 0
            ? attackData
            : additionalHits[rt.hitIndex];

        if (currentHitData == null || currentHitData.timeToChangeAnim <= 0f) return;
        if (ctx.CombatPlanner.AttackNormalizedTime < currentHitData.timeToChangeAnim) return;
        if (!RollChainChance(rt.hitIndex)) return;

        rt.chainedThisHit = true;
        rt.hitIndex = nextIndex;
        LaunchHit(ctx, rt);
    }

    public override bool OnHitFinished(EnemyContext ctx)
    {
        if (additionalHits == null || additionalHits.Length == 0) return false;

        var rt = ctx.CombatPlanner.GetOrCreateRuntime<ComboRuntime>(this);

        if (rt.chainedThisHit) return true;

        int nextIndex = rt.hitIndex + 1;
        if (nextIndex >= additionalHits.Length) return false;
        if (additionalHits[nextIndex] == null) return false;
        if (!RollChainChance(rt.hitIndex)) return false;

        rt.hitIndex = nextIndex;

        if (gapBetweenHits > 0f)
        {
            rt.inGap = true;
            rt.gapTimer = gapBetweenHits;
            return true;
        }

        LaunchHit(ctx, rt);
        return true;
    }

    public override void OnInterrupted(EnemyContext ctx)
    {
        base.OnInterrupted(ctx);
        ctx.CombatPlanner.ClearRuntime(this);
    }

    /// Returns true if the chain from hitIndex → hitIndex+1 should proceed.
    /// Uses chainChancePerHit[hitIndex] if defined and > 0, otherwise falls back to comboChance.
    private bool RollChainChance(int fromHitIndex)
    {
        float chance = comboChance;

        int arrayIndex = fromHitIndex + 1;
        if (chainChancePerHit != null && arrayIndex >= 0 && arrayIndex < chainChancePerHit.Length)
        {
            float perHit = chainChancePerHit[arrayIndex];
            if (perHit > 0f)
                chance = perHit;
        }

        return chance >= 1f || Random.value <= chance;
    }

    private void LaunchHit(EnemyContext ctx, ComboRuntime rt)
    {
        var hit = additionalHits[rt.hitIndex];
        if (hit == null) return;

        rt.chainedThisHit = false;

        FaceTarget(ctx);
        hit.Execute(ctx.Animation.PlayTargetAnimation);
        ctx.CombatPlanner.ContinueComboHit(hit);
    }

    private void FaceTarget(EnemyContext ctx)
    {
        var target = ctx.Perception.CurrentTarget;
        if (target == null) return;

        Vector3 dir = target.position - ctx.Owner.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            ctx.Owner.transform.rotation = Quaternion.LookRotation(dir);

        if (!useHardLook)
            ctx.Movement.ForcedAttackDirection = dir.normalized;
    }
}