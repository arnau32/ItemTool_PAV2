using UnityEngine;
using UnityEngine.AI;

public class ReturnToSpawnState : BaseState<EnemyContext>
{
    private NavMeshAgent _agent;

    private const float ArrivalThreshold = 1.5f;
    private const float ArrivalThreshold2 = ArrivalThreshold * ArrivalThreshold;

    public ReturnToSpawnState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public override void OnEnter()
    {
        _agent = _ctx.Agent;

        _ctx.Perception.EnablePerception();
        _ctx.Movement.EnableMovement();
        _ctx.Movement.SetWalkSpeed();

        _agent.isStopped = false;
        _agent.SetDestination(_ctx.SpawnPosition);
    }

    public override void Tick(float dt)
    {
        Enums.AlertLevel level = _ctx.Perception.AlertLevel;

        if (_ctx.BehaviorProfile.canChase && level == Enums.AlertLevel.Combat)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
            return;
        }

        if (level == Enums.AlertLevel.Alert)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Alert);
            return;
        }

        if (level == Enums.AlertLevel.Suspicious)
        {
            _ctx.StateFactory.Investigate.Configure(activeSearch: false);
            _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
            return;
        }

        Vector3 toSpawn = _ctx.SpawnPosition - _ctx.Owner.transform.position;
        toSpawn.y = 0f;

        if (toSpawn.sqrMagnitude <= ArrivalThreshold2)
        {
            _finiteStateMachine.Set(
                _ctx.StateFactory.GetInitialState(_ctx.BehaviorProfile.startPatrolling));
        }
    }
}