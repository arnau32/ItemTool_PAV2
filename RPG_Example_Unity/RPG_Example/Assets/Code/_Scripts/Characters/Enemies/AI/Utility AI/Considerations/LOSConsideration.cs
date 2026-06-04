using UnityEngine;

[CreateAssetMenu(fileName = "LOS", menuName = "Enemies/Considerations/Line Of Sight", order = 0)]
public class LOSConsideration : UtilityConsideration
{
    [Tooltip("true = only makes high points when there is line of sight.")]
    public bool requireLOS = true;

    public override float Evaluate(in ScoreContext ctx)
    {
        if (requireLOS)
        {
            return ctx.hasLOS ? 1f : 0f;
        }

        // Variant: action better when there is no LOS like flanking
        return ctx.hasLOS ? 0f : 1f;
    }
}