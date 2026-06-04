using UnityEngine;
using UnityEngine.AI;

public class PatrollState : BaseState<EnemyContext>
{
    private enum Phase
    {
        Moving,
        ExecutingRoutine
    }

    private Phase _phase;
    private float _routineTimer;
    private float _routineDuration;
    private IdleRoutine.RoutineType _activeRoutineType;

    private InteractPoint _occupiedPoint;
    private bool _isTurningRight;
    private bool _isPlayingTurnAnim;
    private bool _turnAnimStarted;
    private float _turnPauseTimer;

    private NavMeshAgent _agent;

    private const float TURN_PAUSE_MIN = 1f;
    private const float TURN_PAUSE_MAX = 2.5f;
    private const float LOOK_AROUND_ROTATION_SPEED = 60f; // used for InteractPoint facing
    private const float SWITCH_DIST = 0.8f;

    private static readonly Collider[] _interactBuffer = new Collider[16];

#if UNITY_EDITOR
    public string DebugPhase => _phase.ToString();
    public string DebugRoutine => _phase == Phase.ExecutingRoutine ? _activeRoutineType.ToString() : "—";
    public float DebugRoutineTimer => _routineTimer;
    public float DebugRoutineDuration => _routineDuration;
#endif

    public PatrollState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    public override void OnEnter()
    {
        _agent = _ctx.Agent;
        _ctx.Movement.SetWalkSpeed();
        _phase = Phase.Moving;
        MoveToNextPatrolPoint();
    }

    public override void Tick(float dt)
    {
        if (CheckPerceptionTransitions()) return;

        if (_phase == Phase.Moving)
        {
            TickMoving();
        }
        else
        {
            TickRoutine(dt);
        }
    }

    public override void OnExit()
    {
        base.OnExit();
        if (_isPlayingTurnAnim)
            _ctx.Animation.SetStopRotation();
        _ctx.Movement.EnableTurnMode(false);
        ReleaseInteractPoint();
    }

    #region Phase: Moving

    private void TickMoving()
    {
        if (_agent.pathPending) return;
        if (_agent.remainingDistance > SWITCH_DIST) return;

        if (!HasRoutines())
        {
            MoveToNextPatrolPoint();
            return;
        }

        var routine = PickRoutine();
        BeginRoutine(routine);
    }

    private void MoveToNextPatrolPoint()
    {
        Vector3 next;

        if (_ctx.BehaviorProfile.useAreaPatrol)
        {
            next = UtilsNagu.GetRandomNavmeshPoint(_ctx.PatrolCenter, _ctx.BehaviorProfile.patrolRadius);
            if (next == Vector3.zero) next = _ctx.PatrolCenter;
        }
        else
        {
            next = UtilsNagu.GetRandomNavmeshPoint(_ctx.Owner.transform.position, 10f);
        }

        _ctx.Movement.SafeSetDestination(next);
    }

    #endregion

    #region Phase: Routine

    private void BeginRoutine(IdleRoutine routine)
    {
        _phase = Phase.ExecutingRoutine;
        _activeRoutineType = routine.type;
        _routineDuration = Random.Range(routine.minDuration, routine.maxDuration);
        _routineTimer = 0f;

        _ctx.Movement.SmoothStop();

        switch (routine.type)
        {
            case IdleRoutine.RoutineType.Stand:
                break;

            case IdleRoutine.RoutineType.LookAround:
                _isPlayingTurnAnim = false;
                _turnAnimStarted = false;
                _turnPauseTimer = 0f;
                _isTurningRight = Random.value > 0.5f;
                _isPlayingTurnAnim = true;
                _ctx.Movement.EnableTurnMode(true);
                _ctx.Animation.PlayTargetAnimation(
                    _isTurningRight ? EnemyAnimHashes.HashTurnRight : EnemyAnimHashes.HashTurnLeft,
                    0f,
                    EnemyAnimHashes.LayerOverride);
                break;

            case IdleRoutine.RoutineType.InteractPoint:
                var point = FindNearestInteractPoint();
                if (point != null)
                {
                    _occupiedPoint = point;
                    _occupiedPoint.Occupy();
                    _ctx.Movement.SetWalkSpeed();
                    _ctx.Movement.EnableMovement();
                    _ctx.Movement.SafeSetDestination(point.Position);
                }
                else
                {
                    _activeRoutineType = IdleRoutine.RoutineType.Stand;
                }

                break;

            case IdleRoutine.RoutineType.Patrol:
                _phase = Phase.Moving;
                _ctx.Movement.SetWalkSpeed();
                _ctx.Movement.EnableMovement();
                MoveToNextPatrolPoint();
                break;
        }
    }

    private void TickRoutine(float dt)
    {
        _routineTimer += dt;

        switch (_activeRoutineType)
        {
            case IdleRoutine.RoutineType.Stand:
                break;

            case IdleRoutine.RoutineType.LookAround:
                TickLookAround();
                break;

            case IdleRoutine.RoutineType.InteractPoint:
                TickInteractPoint();
                break;
        }

        if (_routineTimer >= _routineDuration)
        {
            EndRoutine();
        }
    }

    private void TickLookAround()
    {
        if (_isPlayingTurnAnim)
        {
            if (!_turnAnimStarted)
            {
                bool inTransition = _ctx.Animation.Animator.IsInTransition(EnemyAnimHashes.LayerOverride);
                var info = _ctx.Animation.Animator.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerOverride);
                bool inTurn = (info.shortNameHash == EnemyAnimHashes.HashTurnRight
                           || info.shortNameHash == EnemyAnimHashes.HashTurnLeft)
                           && !inTransition;
                if (inTurn)
                    _turnAnimStarted = true;
            }
            else if (_ctx.Animation.IsTurnAnimationDone())
            {
                _isPlayingTurnAnim = false;
                _turnPauseTimer = Random.Range(TURN_PAUSE_MIN, TURN_PAUSE_MAX);
            }
            else
            {
                float dir = _isTurningRight ? 1f : -1f;
                _ctx.Owner.transform.Rotate(0f, _ctx.BehaviorProfile.turnRotationSpeed * dir * Time.deltaTime, 0f, Space.World);
            }
        }
        else if (_turnPauseTimer > 0f)
        {
            _turnPauseTimer -= Time.deltaTime;

            if (_turnPauseTimer <= 0f && _routineTimer < _routineDuration)
            {
                _isTurningRight = !_isTurningRight;
                _isPlayingTurnAnim = true;
                _turnAnimStarted = false;
                _ctx.Animation.PlayTargetAnimation(
                    _isTurningRight ? EnemyAnimHashes.HashTurnRight : EnemyAnimHashes.HashTurnLeft,
                    0f,
                    EnemyAnimHashes.LayerOverride);
            }
        }
    }

    private void TickInteractPoint()
    {
        if (_occupiedPoint == null)
        {
            _activeRoutineType = IdleRoutine.RoutineType.Stand;
            return;
        }

        if (_agent.pathPending) return;
        if (!(_agent.remainingDistance <= SWITCH_DIST)) return;

        _ctx.Movement.SmoothStop();

        var facing = _occupiedPoint.FacingWorld;
        if (facing.sqrMagnitude > 0.001f)
        {
            var rot = Quaternion.LookRotation(facing);
            _ctx.Owner.transform.rotation = Quaternion.RotateTowards(
                _ctx.Owner.transform.rotation,
                rot,
                LOOK_AROUND_ROTATION_SPEED * Time.deltaTime);
        }
    }

    private void EndRoutine()
    {
        if (_isPlayingTurnAnim)
            _ctx.Animation.SetStopRotation();
        _ctx.Movement.EnableTurnMode(false);
        ReleaseInteractPoint();
        _phase = Phase.Moving;
        _ctx.Movement.SetWalkSpeed();
        _ctx.Movement.EnableMovement();
        MoveToNextPatrolPoint();
    }

    #endregion

    #region Helpers

    private bool CheckPerceptionTransitions()
    {
        Enums.AlertLevel level = _ctx.Perception.AlertLevel;

        if (_ctx.BehaviorProfile.canChase && level == Enums.AlertLevel.Combat)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
            return true;
        }

        if (level == Enums.AlertLevel.Alert)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Alert);
            return true;
        }

        if (level == Enums.AlertLevel.Suspicious)
        {
            _ctx.StateFactory.Investigate.Configure(activeSearch: false);
            _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
            return true;
        }

        return false;
    }

    private bool HasRoutines()
    {
        var routines = _ctx.BehaviorProfile.idleRoutines;
        return routines != null && routines.Length > 0;
    }

    private IdleRoutine PickRoutine()
    {
        var routines = _ctx.BehaviorProfile.idleRoutines;

        float total = 0f;
        for (int i = 0; i < routines.Length; i++) total += routines[i].weight;

        if (total <= 0f) return routines[0];

        float roll = Random.value * total;
        float acc = 0f;

        for (int i = 0; i < routines.Length; i++)
        {
            acc += routines[i].weight;
            if (roll <= acc) return routines[i];
        }

        return routines[routines.Length - 1];
    }

    private InteractPoint FindNearestInteractPoint()
    {
        Vector3 origin = _ctx.Owner.transform.position;
        float radius = _ctx.BehaviorProfile.interactPointSearchRadius;

        int count = Physics.OverlapSphereNonAlloc(origin, radius, _interactBuffer);

        InteractPoint best = null;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = _interactBuffer[i];
            if (col == null) continue;

            var point = col.GetComponent<InteractPoint>();
            if (point == null || !point.IsAvailable) continue;

            float d2 = (point.Position - origin).sqrMagnitude;
            if (d2 >= bestD2) continue;

            bestD2 = d2;
            best = point;
        }

        return best;
    }

    private void ReleaseInteractPoint()
    {
        if (_occupiedPoint == null) return;
        _occupiedPoint.Release();
        _occupiedPoint = null;
    }

    #endregion
}