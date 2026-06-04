using UnityEngine;

[CreateAssetMenu(fileName = "AngleBehinf", menuName = "Enemies/Considerations/Angle Behind", order = 0)]
public class AngleBehindConsideration : UtilityConsideration
{
    [Tooltip("Tolerancia lateral para considerar que estoy detrás (grados).")]
    public float behindHalfAngle = 45f;

    public override float Evaluate(in ScoreContext ctx)
    {
        // ctx.angleDegree definido como -180..180 desde frente del enemigo.
        // Detrás ~ 180 / -180.
        float angleFromBehind = Mathf.Min(
            Mathf.Abs(ctx.angleDegree - 180f),
            Mathf.Abs(ctx.angleDegree + 180f)
        );

        float normalized = 1f - angleFromBehind / behindHalfAngle;
        return Mathf.Clamp01(normalized);
    }
}