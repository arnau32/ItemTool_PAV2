using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class InvestigateState : BaseState<EnemyContext>
{
    private NavMeshAgent _agent;
    private Vector3 _poi;
    private float _timeout;
    private float _timer;

    private int _probeIndex;
    private Vector3[] _probes;
    private Vector3 _currentDestination;

    private bool _activeSearch;

    private string _name;

    private const float TIMEOUT_MIN = 2.5f;
    private const float TIMEOUT_MAX = 5.0f;
    private const float TIMEOUT_SIMPLE = 4.0f;
    private const int PROBE_COUNT = 3;
    private const float PROBE_RADIUS = 5.5f;

    private const float ARRIVAL_DIST = 1.0f;
    private const float ARRIVAL_DIST_SQR = ARRIVAL_DIST * ARRIVAL_DIST;

    private const float AUDIO_REROUTE_DIST_SQR = 1f;

    public InvestigateState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public void Configure(bool activeSearch)
    {
        _activeSearch = activeSearch;
    }

    public override void OnEnter()
    {
        _agent = _ctx.Agent;
        _name = _ctx.Owner.name;

        _agent.ResetPath();

        _poi = _ctx.Perception.LastKnownPosition;
        _timer = 0f;
        _currentDestination = Vector3.zero;

        _ctx.Movement.EnableMovement();
        _ctx.Movement.SetWalkSpeed();

        if (_activeSearch)
        {
            _timeout = Random.Range(TIMEOUT_MIN, TIMEOUT_MAX);
            _probeIndex = 0;
            _probes = BuildProbes(_poi, PROBE_COUNT, PROBE_RADIUS);
        }
        else
        {
            _timeout = TIMEOUT_SIMPLE;
            _probes = null;
            _ctx.Audio.PlaySuspicious();
        }

        SetDestination(_poi);
    }

    public override void Tick(float dt)
    {
        Enums.AlertLevel level = _ctx.Perception.AlertLevel;

        if (_ctx.Perception.HasVisual && _ctx.Perception.CurrentTarget != null)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
            return;
        }

        if (level == Enums.AlertLevel.Combat)
        {
            if (_ctx.Perception.CurrentTarget != null)
            {
                _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
                return;
            }
        }

        if (!_activeSearch && level == Enums.AlertLevel.Alert)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Alert);
            return;
        }

        _timer += dt;

        if (_timer >= _timeout)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.ReturnToSpawn);
            return;
        }

        if (_ctx.Perception.HasAudio)
        {
            Vector3 audioPos = _ctx.Perception.LastKnownPosition;
            Vector3 diff = audioPos - _currentDestination;
            diff.y = 0f;

            if (diff.sqrMagnitude > AUDIO_REROUTE_DIST_SQR)
            {
                SetDestination(audioPos);
            }
        }

        if (!_activeSearch) return;

        if (_currentDestination == Vector3.zero) return;

        Vector3 toTarget = _ctx.Owner.transform.position - _currentDestination;
        toTarget.y = 0f;
        float distSqr = toTarget.sqrMagnitude;

        if (distSqr > ARRIVAL_DIST_SQR) return;
        AdvanceProbe();
    }

    public override void OnExit()
    {
        base.OnExit();

        if (_activeSearch)
        {
            _ctx.Perception.ClearLastTarget();
        }

        _agent.ResetPath();
    }

    #region Helpers

    private void AdvanceProbe()
    {
        while (_probeIndex < _probes.Length)
        {
            Vector3 pos = _probes[_probeIndex];
            _probeIndex++;

            if (pos == Vector3.zero) continue;

            SetDestination(pos);
            return;
        }

        if (_currentDestination == Vector3.zero)
        {
            SetDestination(_poi);
        }
    }

    private void SetDestination(Vector3 pos)
    {
        if (pos == Vector3.zero) return;

        _currentDestination = pos;
        _agent.isStopped = false;
        _ctx.Movement.SafeSetDestination(pos);
    }

    private static Vector3[] BuildProbes(Vector3 center, int count, float radius)
    {
        var arr = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = UtilsNagu.GetRandomNavmeshPoint(center, radius);
        }

        return arr;
    }

    #endregion
}