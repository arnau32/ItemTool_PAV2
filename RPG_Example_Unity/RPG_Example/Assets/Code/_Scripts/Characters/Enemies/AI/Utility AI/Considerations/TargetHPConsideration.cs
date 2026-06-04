using UnityEngine;

[CreateAssetMenu(fileName = "TargetHP", menuName = "Enemies/Considerations/Target HP", order = 0)]
public class TargetHPConsideration : UtilityConsideration
{
    [Tooltip("true = higher when target has low life (finish off).")]
    public bool preferLowTargetHP = true;

    public AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 0);

    public override float Evaluate(in ScoreContext ctx)
    {
        float hp = Mathf.Clamp01(ctx.targetHP);

        if (preferLowTargetHP)
        {
            hp = 1f - hp;
        }

        float v = curve.Evaluate(hp);
        return Mathf.Clamp01(v);
    }
}