using UnityEngine;

[CreateAssetMenu(fileName = "DistanceRange", menuName = "Enemies/Considerations/Distance Range", order = 0)]
public class DistanceRangeConsideration : UtilityConsideration
{
    public float minDistance = 0.5f;
    public float maxDistance = 4f;

    [Tooltip("Curve to get the normalized value between 0..1.")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        float d = ctx.distance;

        if (d <= minDistance) return 0f;
        if (d >= maxDistance) return 0f;

        float t = Mathf.InverseLerp(minDistance, maxDistance, d);
        float v = curve.Evaluate(Mathf.Clamp01(t));
        return Mathf.Clamp01(v);
    }
}