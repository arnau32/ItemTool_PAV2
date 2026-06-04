using UnityEngine;

public class ChasingState : BaseState<EnemyContext>
{
    private float _loseSightGrace = 1.5f;
    private float _timerLoseSight;
    private Transform _transform;
    private float _maxChaseDist2;
    private bool _isInterceptor;

    public ChasingState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx) { }

    public override void OnEnter()
    {
        base.OnEnter();

        _ctx.Movement.EnableMovement();
        _ctx.Movement.target = _ctx.Perception.CurrentTarget;

        // Release any held attack token — the enemy is no longer in melee range.
        _ctx.CombatPlanner.ForceRelease();

        _timerLoseSight = 0f;
        _ctx.Movement.SetRunningSpeed();
        _transform = _ctx.Transform;

        float maxChase = _ctx.BehaviorProfile.maxChaseDistance;
        _maxChaseDist2 = maxChase * maxChase;

        // Decide role: interceptor tries to cut off the player's path,
        // chaser follows directly. Role is fixed per chase entry so enemies
        // don't flip roles every frame.
        _isInterceptor = Random.value < _ctx.BehaviorProfile.interceptChance;

        _ctx.Audio.PlayAlert();

        if (_ctx.Perception.CurrentTarget == null)
        {
            var lastKnown = _ctx.Perception.LastKnownPosition;
            if (lastKnown != Vector3.zero)
                _ctx.Movement.SafeSetDestination(lastKnown);
        }
    }

    public override void Tick(float dt)
    {
        bool hasTarget = _ctx.Perception.CurrentTarget != null;

        if (!hasTarget)
        {
            GoInvestigate();
            return;
        }

        Vector3 toTarget = _ctx.Perception.CurrentTarget.position - _transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > _maxChaseDist2)
        {
            GoInvestigate();
            return;
        }

        if (_isInterceptor)
            _ctx.Movement.HandleInterceptMovement(_ctx.BehaviorProfile.interceptPredictionTime);
        else
            _ctx.Movement.HandleChaseMovement();

        if (_ctx.BehaviorProfile.canEnterCombat && _ctx.Movement.DistanceToTarget() <= _ctx.CombatPlanner.combatRange)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.CombatWander);
            return;
        }

        if (_ctx.Perception.HasVisual)
        {
            _timerLoseSight = 0f;
            return;
        }

        _timerLoseSight += dt;

        if (_timerLoseSight >= _loseSightGrace)
            GoInvestigate();
    }

    private void GoInvestigate()
    {
        _ctx.StateFactory.Investigate.Configure(activeSearch: true);
        _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
    }
}