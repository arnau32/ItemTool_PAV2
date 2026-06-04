using UnityEngine;

public class KnockbackState : BaseState<EnemyContext>
{
    private bool _animStarted;
    private float _remainingTime;
    private bool _fromParry;

    public KnockbackState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx) { }

    public void Configure(bool fromParry)
    {
        _fromParry = fromParry;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        int hash = _fromParry ? EnemyAnimHashes.HashKnockbackParry : EnemyAnimHashes.HashKnockback;
        _ctx.Animation.PlayTargetAnimation(hash, 0f, EnemyAnimHashes.LayerOverride);
        _ctx.Movement.DisableMovement();
        _ctx.Movement.HardLookAtTarget();
        
        _animStarted = false;
    }

    public override void Tick(float dt)
    {
        if (!_animStarted)
        {
            var animator = _ctx.Animation.Animator;
            var st = animator.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerOverride);
            int expectedHash = _fromParry ? EnemyAnimHashes.HashKnockbackParry : EnemyAnimHashes.HashKnockback;

            if (st.shortNameHash != expectedHash) return;
            
            _animStarted = true;
            _remainingTime = st.length;
            return;
        }

        _remainingTime -= Time.deltaTime;
        
        if (!(_remainingTime <= 0f)) return;
        
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
        _fromParry = false;
        _ctx.Movement.EnableAgent();

        if (_ctx.CombatPlanner.TryReengageAfterHit()) return;
    }
}