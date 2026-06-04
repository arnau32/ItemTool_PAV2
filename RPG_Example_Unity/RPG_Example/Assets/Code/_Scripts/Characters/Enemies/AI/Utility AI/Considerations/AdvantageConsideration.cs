using UnityEngine;

// - Pushes aggresivity when winning and makes prudent when loosing
[CreateAssetMenu(fileName = "AdvantageConsideration", menuName = "Enemies/Considerations/Advantage", order = 0)]
public class AdvantageConsideration : UtilityConsideration
{
    [Tooltip("if true, goes higher with positive advantatge (attack). if false, the other way around (defense).")]
    public bool preferPositiveAdvantage = true;

    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float adv = Mathf.Clamp01(ctx.advantage);

        if (!preferPositiveAdvantage)
        {
            adv = 1f - adv;
        }

        float v = curve.Evaluate(adv);
        return Mathf.Clamp01(v);
    }
}