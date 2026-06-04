
public interface ICombatMotionData
{
    bool UseRootMotionMultiplier { get; }
    float EvaluateRootMotionMultiplier(float t01);
    bool UseProceduralForwardMove { get; }
    float ProceduralForwardMoveDistance { get; }
    float EvaluateProceduralForwardProgress01(float t01);
}
