using UnityEngine;

[CreateAssetMenu(fileName = "SelfHP", menuName = "Enemies/Considerations/Self HP", order = 0)]
public class SelfHPConsideration : UtilityConsideration
{
    [Tooltip("true = higher with alot of life. false = with low hp.")]
    public bool preferHighHP = true;

    [Tooltip("Curva about own life 0..1 (applies inversion if neeeded).")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float hp = Mathf.Clamp01(ctx.selfHP);
        if (!preferHighHP)
        {
            hp = 1f - hp;
        }

        float v = curve.Evaluate(hp);
        return Mathf.Clamp01(v);
    }
}