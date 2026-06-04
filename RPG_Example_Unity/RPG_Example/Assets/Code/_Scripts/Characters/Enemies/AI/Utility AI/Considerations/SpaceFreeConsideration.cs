using UnityEngine;

[CreateAssetMenu(fileName = "SpaceFree", menuName = "Enemies/Considerations/Space Free")]
public class SpaceFreeConsideration : UtilityConsideration
{
    [Tooltip("If true, only make sense when there is space (for dashes, orbit etc).")]
    public bool preferFreeSpace = true;

    public override float Evaluate(in ScoreContext ctx)
    {
        if (preferFreeSpace)
        {
            return ctx.spaceFree ? 1f : 0f;
        }

        // For Actions better in reduced spaces or "pasillos"
        return ctx.spaceFree ? 0f : 1f;
    }
}