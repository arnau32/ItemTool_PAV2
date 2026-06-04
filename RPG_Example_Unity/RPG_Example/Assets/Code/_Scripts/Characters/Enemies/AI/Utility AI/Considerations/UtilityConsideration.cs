using Gameplay.Enemies;
using UnityEngine;

public abstract class UtilityConsideration : ScriptableObject
{
    public abstract float Evaluate(in ScoreContext ctx);

#if UNITY_EDITOR
    public virtual float EvaluateDebug(float x) => x;
#endif
}
