using UnityEngine;

[CreateAssetMenu(fileName = "SelfStamina", menuName = "Enemies/Considerations/Self Stamina", order = 0)]
public class SelfStaminaConsideration : UtilityConsideration
{
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float stam = Mathf.Clamp01(ctx.selfStamine);
        float v = curve.Evaluate(stam);
        return Mathf.Clamp01(v);
    }
}