
using UnityEngine;

public class DeadState : BaseState<EnemyContext>
{
    public DeadState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx) { }

    public override void OnEnter()
    {
        base.OnEnter();

        _ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashDead, 0.2f, EnemyAnimHashes.LayerOverride);
        _ctx.Health.SetInvulnerable(true);
        _ctx.Movement.DisableMovement();
        _ctx.Movement.HardLookAtTarget();

        
        WaitExtension.WaitForAnimationOnLayerWithDelay(_ctx.Animation.Animator, EnemyAnimHashes.LayerOverride, 0.8f, OnAnimationFinished);
    }

    private void OnAnimationFinished()
    {
        if (_ctx.Owner == null || !_ctx.Owner.activeSelf) return;

        _ctx.NotifyDeathComplete();
    }
}