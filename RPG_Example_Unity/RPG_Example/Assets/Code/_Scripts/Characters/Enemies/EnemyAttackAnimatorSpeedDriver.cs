using UnityEngine;

public sealed class EnemyAttackAnimatorSpeedDriver : MonoBehaviour
{
    private EnemyContext _ctx;
    private EnemyCombatPlanner _planner;

    public void Initialize(EnemyContext ctx)
    {
        _ctx = ctx;
        _planner = _ctx != null ? _ctx.CombatPlanner : null;

        if (_ctx != null && _ctx.Animation != null)
        {
            _ctx.Animation.ResetAttackSpeed();
        }
    }

    private void OnDisable()
    {
        if (_ctx != null && _ctx.Animation != null)
        {
            _ctx.Animation.ResetAttackSpeed();
        }
    }

    private void FixedUpdate()
    {
        if (_ctx == null || _ctx.Animation == null || _planner == null)
            return;

        if (!_ctx.Animation.IsAttacking || !_planner.IsAttackRuntimeActive)
        {
            _ctx.Animation.ResetAttackSpeed();
            return;
        }

        var atk = _planner.ActiveAttack;
        if (atk == null || !atk.useAnimatorSpeedCurve)
        {
            _ctx.Animation.ResetAttackSpeed();
            return;
        }

        var normalized = Mathf.Clamp01(_planner.AttackNormalizedTime);
        var speed = atk.EvaluateAnimatorSpeed(normalized);

        _ctx.Animation.SetAttackSpeedMultiplier(speed);
    }
}