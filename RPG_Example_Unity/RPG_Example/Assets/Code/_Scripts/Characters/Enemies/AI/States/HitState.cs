using UnityEngine;

public class HitState : BaseState<EnemyContext>
{
    private bool _animStarted;
    private float _remainingTime;
    private int _nRand;
    
    public HitState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public override void OnEnter()
    {
        _nRand = Random.Range(1, 3);
        _ctx.Animation.PlayHitAnimation(_nRand);
        _ctx.Audio.PlayHitReaction();
        _animStarted = false;
    }
    
    public override void Tick(float dt)
    {
        if (!_animStarted)
        {
            var st = _ctx.Animation.Animator.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerUpwards);
            if (st.shortNameHash != EnemyAnimHashes.HashHit(_nRand)) return;

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
        base.OnExit();
        if (_ctx.CombatPlanner.TryReengageAfterHit()) return;
    }
}