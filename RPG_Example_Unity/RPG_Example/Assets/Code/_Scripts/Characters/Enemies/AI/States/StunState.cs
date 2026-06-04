// Enemy stun after poise break. Three-phase animation: Start → Loop → End.
// Loop plays until stunDuration elapses, then KD_Done triggers the End clip.
// WaitForAnimation waits for End to finish before transitioning to next state.
public class StunState : BaseState<EnemyContext>
{
    private float _stunDuration;
    private float _timer;
    private bool _triggeringEnd;

    public StunState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public void Configure(float duration)
    {
        _stunDuration = duration;
    }

    public override void OnEnter()
    {
        base.OnEnter();

        _timer = 0f;
        _triggeringEnd = false;

        _ctx.CombatPlanner.ForceRelease();
        _ctx.Movement.DisableMovement();

        _ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashStun, 0.1f, EnemyAnimHashes.LayerOverride);

        _ctx.Feedbacks?.PlayStunEnter();
    }

    public override void Tick(float dt)
    {
        if (_triggeringEnd) return;

        _timer += dt;

        if (_timer < _stunDuration) return;

        _triggeringEnd = true;

        _ctx.Animation.Animator.SetTrigger(SharedHashes.HashKnockdownDone);

        // Wait for the End clip to finish before changing state.
        WaitExtension.WaitForAnimationOnLayer(
            _ctx.Animation.Animator,
            EnemyAnimHashes.LayerOverride,
            OnEndFinished);
    }

    private void OnEndFinished()
    {
        if (!_ctx.Health.IsAlive) return;

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
        base.OnExit();

        _ctx.Poise.NotifyStunEnded();
        _ctx.Movement.EnableMovement();
        _ctx.CombatPlanner.TryReengageAfterHit();
    }
}