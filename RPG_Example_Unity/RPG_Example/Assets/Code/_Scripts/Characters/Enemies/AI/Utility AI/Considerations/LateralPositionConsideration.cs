using UnityEngine;

[CreateAssetMenu(fileName = "LateralPositionConsideration", menuName = "Enemies/Considerations/Lateral Position", order = 0)]
public class LateralPositionConsideration : UtilityConsideration
{
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float lateral = Mathf.Abs(Mathf.Sin(ctx.angleDegree * Mathf.Deg2Rad));
        float v = curve.Evaluate(Mathf.Clamp01(lateral));
        return Mathf.Clamp01(v);
    }
}