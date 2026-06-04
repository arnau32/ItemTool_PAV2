using UnityEngine;

[CreateAssetMenu(fileName = "AngleFrontal", menuName = "Enemies/Considerations/Angle Frontal", order = 0)]
public class AngleFrontalConsideration : UtilityConsideration
{
    [Tooltip("Medio ángulo frontal efectivo (en grados).")]
    public float frontalHalfAngle = 45f;

    public override float Evaluate(in ScoreContext ctx)
    {
        float normalized = 1f - Mathf.Abs(ctx.angleDegree) / frontalHalfAngle;
        return Mathf.Clamp01(normalized);
    }
}