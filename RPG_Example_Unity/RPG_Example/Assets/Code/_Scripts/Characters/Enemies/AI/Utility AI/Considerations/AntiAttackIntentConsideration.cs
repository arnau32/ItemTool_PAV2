using UnityEngine;

[CreateAssetMenu(fileName = "AntiAttackIntentConsideration", menuName = "Enemies/Considerations/Anti Attack Intent",
    order = 0)]
public class AntiAttackIntentConsideration : UtilityConsideration
{
    public AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 0);

    public override float Evaluate(in ScoreContext ctx)
    {
        float intent = Mathf.Clamp01(ctx.attackIntent01);
        float v = curve.Evaluate(intent);
        return Mathf.Clamp01(v);
    }
}