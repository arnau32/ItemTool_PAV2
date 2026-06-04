using UnityEngine;

[CreateAssetMenu(fileName = "AttackIntentConsideration", menuName = "Enemies/Considerations/Attack Intent")]
public class AttackIntentConsideration : UtilityConsideration
{
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        var v = curve.Evaluate(ctx.attackIntent01);

        return Mathf.Clamp01(v);
    }
}