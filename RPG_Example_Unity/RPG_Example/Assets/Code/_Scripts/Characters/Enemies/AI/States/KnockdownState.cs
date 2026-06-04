using UnityEngine;

public class KnockdownState : BaseState<EnemyContext>
{
    private float _knockdDownTime = 2f;
    private float _currentTime = 0f;
    private bool _gettingUp = false;
    private Transform _targetAtKnockdown;

    public KnockdownState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx) { }

    public KnockdownState(StateMachine fsm, EnemyContext ctx, float timeKnockdown) : base(fsm, ctx)
    {
        _knockdDownTime = timeKnockdown;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        _targetAtKnockdown = _ctx.Perception.CurrentTarget;
        _ctx.Movement.DisableMovement();
        _ctx.Movement.HardLookAtTarget();
        _ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashKnockdown, .2f, EnemyAnimHashes.LayerOverride);
    }

    public override void Tick(float dt)
    {
        _currentTime += dt;

        if (!(_currentTime >= _knockdDownTime) || _gettingUp) return;

        _gettingUp = true;

        _ctx.Animation.Animator.SetTrigger(SharedHashes.HashKnockdownDone);

        WaitExtension.WaitForAnimationOnLayer(_ctx.Animation.Animator, EnemyAnimHashes.LayerOverride, DecideNext);
    }

    private void DecideNext()
    {
        if (_targetAtKnockdown != null && _targetAtKnockdown.gameObject.activeInHierarchy)
        {
            _ctx.Perception.RestoreTarget(_targetAtKnockdown);
        }

        var next = EnemyTransitions.DecidePostHit(_ctx);

        if (next != null)
        {
            _finiteStateMachine.Set(next);
            return;
        }

        _finiteStateMachine.Set(_ctx.StateFactory.Idle);
    }

    public override void OnExit()
    {
        _ctx.Movement.EnableAgent();

        _currentTime = 0f;
        _gettingUp = false;
        _targetAtKnockdown = null;
    }
}