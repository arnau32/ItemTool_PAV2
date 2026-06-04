using UnityEngine;

// - The objective behind this considerations is to punish whiffs or startups.

[CreateAssetMenu(fileName = "OpportunityWindow", menuName = "Enemies/Considerations/Opportunity Window", order = 0)]
public class OpportunityWIndowConsideration : UtilityConsideration
{
    [Range(0f, 1f)] public float windupWeight = 0.4f;

    [Range(0f, 1f)] public float recoveryWeight = 0.6f;

    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float w = Mathf.Clamp01(ctx.targetWindup);
        float r = Mathf.Clamp01(ctx.targetRecovery);

        float opp = w * windupWeight + r * recoveryWeight;
        float v = curve.Evaluate(Mathf.Clamp01(opp));

        return Mathf.Clamp01(v);
    }
}