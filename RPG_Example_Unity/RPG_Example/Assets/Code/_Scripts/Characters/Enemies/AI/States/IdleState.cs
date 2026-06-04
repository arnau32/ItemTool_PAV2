using UnityEngine;

public class IdleState : BaseState<EnemyContext>
{
    #region Fields

    private float _routineTimer;
    private float _routineDuration;
    private IdleRoutine.RoutineType _activeRoutineType;

    private bool _isTurningRight;
    private bool _isPlayingTurnAnim;
    private bool _turnAnimStarted;
    private float _turnPauseTimer;

    private float _idleSoundTimer;

    private const float IdleSoundIntervalMin = 6f;
    private const float IdleSoundIntervalMax = 14f;
    private const float TURN_PAUSE_MIN = 1f;
    private const float TURN_PAUSE_MAX = 2.5f;

    #endregion

#if UNITY_EDITOR
    public string DebugRoutine => _activeRoutineType.ToString();
    public float DebugRoutineTimer => _routineTimer;
    public float DebugRoutineDuration => _routineDuration;
#endif

    public IdleState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
    }

    #region Unity Callbacks

    public override void OnEnter()
    {
        base.OnEnter();
        _idleSoundTimer = Random.Range(IdleSoundIntervalMin, IdleSoundIntervalMax);
        BeginRoutine();
    }

    public override void OnExit()
    {
        if (_isPlayingTurnAnim)
            _ctx.Animation.SetStopRotation();
        _ctx.Movement.EnableTurnMode(false);
    }

    public override void Tick(float dt)
    {
        if (_ctx.BehaviorProfile.canChase || _ctx.BehaviorProfile.reactsToSound)
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
        }

        TickRoutine(dt);

        _idleSoundTimer -= dt;
        if (!(_idleSoundTimer <= 0f)) return;

        _ctx.Audio.PlayIdle();
        _idleSoundTimer = Random.Range(IdleSoundIntervalMin, IdleSoundIntervalMax);
    }

    #endregion

    #region Routine

    private void BeginRoutine()
    {
        _isPlayingTurnAnim = false;
        _turnAnimStarted = false;
        _turnPauseTimer = 0f;

        if (!HasRoutines())
        {
            _activeRoutineType = IdleRoutine.RoutineType.Stand;
            _routineDuration = Random.Range(3f, 7f);
            _routineTimer = 0f;
            _ctx.Movement.EnableTurnMode(false);
            return;
        }

        var routine = PickRoutine();
        _activeRoutineType = routine.type == IdleRoutine.RoutineType.Patrol
            ? IdleRoutine.RoutineType.Stand
            : routine.type;
        _routineDuration = Random.Range(routine.minDuration, routine.maxDuration);
        _routineTimer = 0f;

        if (_activeRoutineType == IdleRoutine.RoutineType.LookAround)
        {
            _isTurningRight = Random.value > 0.5f;
            _isPlayingTurnAnim = true;
            _ctx.Movement.EnableTurnMode(true);
            _ctx.Animation.PlayTargetAnimation(
                _isTurningRight ? EnemyAnimHashes.HashTurnRight : EnemyAnimHashes.HashTurnLeft,
                0f,
                EnemyAnimHashes.LayerOverride);
        }
        else
        {
            _ctx.Movement.EnableTurnMode(false);
        }
    }

    private void TickRoutine(float dt)
    {
        if (_activeRoutineType == IdleRoutine.RoutineType.LookAround)
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
                    _ctx.Owner.transform.Rotate(0f, _ctx.BehaviorProfile.turnRotationSpeed * dir * dt, 0f, Space.World);
                }
            }
            else if (_turnPauseTimer > 0f)
            {
                _turnPauseTimer -= dt;

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

        _routineTimer += dt;
        if (_routineTimer >= _routineDuration && !_isPlayingTurnAnim && _turnPauseTimer <= 0f)
            BeginRoutine();
    }

    #endregion

    #region Helpers

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

    #endregion
}
