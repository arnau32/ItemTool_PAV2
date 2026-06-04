using UnityEngine;
using UnityEngine.AI;

/// Enemy has confirmed a stimulus (AlertLevel.Alert).
/// Runs toward the last known position with weapon ready.
/// On POI arrival with no visual → active probe search.
public class AlertState : BaseState<EnemyContext>
{
    private NavMeshAgent _agent;
    private Vector3 _poi;
    private bool _poiReached;

    private string _name;

    private const float POI_ARRIVAL_DIST = 1.2f;

    public AlertState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();

        _agent = _ctx.Agent;
        _poiReached = false;
        _poi = _ctx.Perception.LastKnownPosition;
        _name = _ctx.Owner.name;

        _ctx.Movement.EnableMovement();
        _ctx.Movement.SmoothToRun();

        if (_poi != Vector3.zero)
        {
            _ctx.Movement.SafeSetDestination(_poi);
        }

        _ctx.Audio.PlayAlert();
    }

    public override void Tick(float dt)
    {
        Enums.AlertLevel level = _ctx.Perception.AlertLevel;

        if (level == Enums.AlertLevel.Combat || _ctx.Perception.HasVisual)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
            return;
        }

        if (level == Enums.AlertLevel.Suspicious)
        {
            _ctx.StateFactory.Investigate.Configure(activeSearch: false);
            _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
            return;
        }

        if (level == Enums.AlertLevel.Unaware)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.GetInitialState(_ctx.BehaviorProfile.startPatrolling));
            return;
        }

        if (_ctx.Perception.HasAudio)
        {
            _poi = _ctx.Perception.LastKnownPosition;
            _ctx.Movement.SafeSetDestination(_poi);
            _poiReached = false;
        }

        if (!_poiReached && !_agent.pathPending && _agent.remainingDistance <= POI_ARRIVAL_DIST)
        {
            _poiReached = true;
            _ctx.StateFactory.Investigate.Configure(activeSearch: true);
            _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
        }
    }

    public override void OnExit()
    {
        base.OnExit();
        _agent.ResetPath();
    }
}