using UnityEngine;

public class SpawnState : BaseState<EnemyContext>
{
    private bool _animStarted;
    private float _remainingTime;
    private float _detectTimeout;
    private ITargetable _postSpawnTarget;

    private const float DETECT_TIMEOUT = 0.5f;

    public SpawnState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx) { }

    // Set before entering this state to force the enemy into combat immediately after spawn.
    public void SetPostSpawnTarget(ITargetable target) => _postSpawnTarget = target;

    public override void OnEnter()
    {
        _animStarted = false;
        _detectTimeout = DETECT_TIMEOUT;
        _ctx.Health.SetInvulnerable(true);
        _ctx.Movement.DisableMovement();
        _ctx.Animation.PlayTargetAnimation(EnemyAnimHashes.HashSpawn, 0f, EnemyAnimHashes.LayerOverride);
        _ctx.Feedbacks?.PlaySpawn();
    }

    public override void Tick(float dt)
    {
        if (!_animStarted)
        {
            var st = _ctx.Animation.Animator.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerOverride);
            if (st.shortNameHash == EnemyAnimHashes.HashSpawn)
            {
                _animStarted = true;
                _remainingTime = st.length;
                return;
            }

            _detectTimeout -= dt;
            if (_detectTimeout > 0f) return;

            ExitToInitialState();
            return;
        }

        _remainingTime -= dt;

        if (_remainingTime > 0f) return;

        ExitToInitialState();
    }

    public override void OnExit()
    {
        _ctx.Health.SetInvulnerable(false);
        _ctx.Movement.EnableAgent();

        // Push the Override layer past normalizedTime=1 so any HasExitTime
        // transition fires immediately and the layer doesn't keep looping spawn.
        // The real fix is disabling Loop Time on the spawn clip in the Animator.
        var anim = _ctx.Animation.Animator;
        var info = anim.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerOverride);
        if (info.shortNameHash == EnemyAnimHashes.HashSpawn)
            anim.Play(EnemyAnimHashes.HashSpawn, EnemyAnimHashes.LayerOverride, 1f);
    }

    private void ExitToInitialState()
    {
        if (_postSpawnTarget != null)
        {
            var target = _postSpawnTarget;
            _postSpawnTarget = null;
            _ctx.Perception.ForceTarget(target);
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
        }
        else
        {
            _finiteStateMachine.Set(_ctx.StateFactory.GetInitialState(_ctx.BehaviorProfile.startPatrolling));
        }
    }
}
