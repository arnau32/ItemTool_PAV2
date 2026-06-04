using UnityEngine;

[CreateAssetMenu(fileName = "Player Retreat Consideration", menuName = "Enemies/Considerations/Player Retreat")]
public class PlayerRetreatConsideration : UtilityConsideration
{
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(in ScoreContext ctx)
    {
        var target = ctx.enemyContext.Perception.CurrentTargetableTarget;
        if (target == null) return 0f;

        Vector3 playerVel = target.Velocity;
        if (playerVel.sqrMagnitude < 0.01f) return 0f;

        Vector3 toEnemy = (ctx.enemyContext.Owner.transform.position - target.Transform.position).normalized;

        var retreat = Vector3.Dot(playerVel.normalized, -toEnemy);

        retreat = Mathf.Clamp01((retreat + 1f) * 0.5f);

        return curve.Evaluate(retreat);
    }
}