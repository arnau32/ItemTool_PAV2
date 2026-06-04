using UnityEngine;

public class AttackAnimatorSpeedDriver : MonoBehaviour
{
    private PlayerContext _ctx;

    public void Initialize(PlayerContext ctx)
    {
        _ctx = ctx;

        _ctx.Animation.ResetAttackSpeed();
    }

    private void OnDisable()
    {
        _ctx.Animation.ResetAttackSpeed();
    }

    private void FixedUpdate()
    {
        if (_ctx == null || _ctx.Animation == null || _ctx.ComboController == null)
            return;

        if (!_ctx.Animation.IsAttacking)
        {
            _ctx.Animation.ResetAttackSpeed();
            return;
        }

        var atk = _ctx.ComboController.CurrentAttack;
        if (atk == null || !atk.useAnimatorSpeedCurve)
        {
            _ctx.Animation.ResetAttackSpeed();
            return;
        }

        var animator = _ctx.Animation.Animator;

        var st = animator.IsInTransition(PlayerAnimHashes.LayerOverride)
            ? animator.GetNextAnimatorStateInfo(PlayerAnimHashes.LayerOverride)
            : animator.GetCurrentAnimatorStateInfo(PlayerAnimHashes.LayerOverride);

        var normalized = st.normalizedTime;

        normalized = normalized - Mathf.Floor(normalized);

        var speed = atk.EvaluateAnimatorSpeed(normalized);
        _ctx.Animation.SetAttackSpeedMultiplier(speed);
    }
}