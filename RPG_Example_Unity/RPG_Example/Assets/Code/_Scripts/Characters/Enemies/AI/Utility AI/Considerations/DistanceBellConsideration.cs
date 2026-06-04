using UnityEngine;

[CreateAssetMenu(fileName = "DistanceBell", menuName = "Enemies/Considerations/Distance Bell", order = 0)]
public class DistanceBellConsideration : UtilityConsideration
{
    [Tooltip("Optimal distance for this action (m).")]
    public float optimalRange = 2f;

    [Tooltip("Bell widths (bigger means more tolerance).")]
    public float rangeSigma = 0.75f;

    public override float Evaluate(in ScoreContext ctx)
    {
        // Bell devuelve algo tipo 0..1 centrado en optimalRange.
        float bell = UtilsNagu.Bell(ctx.distance, optimalRange, rangeSigma);
        return Mathf.Clamp01(bell);
    }
}