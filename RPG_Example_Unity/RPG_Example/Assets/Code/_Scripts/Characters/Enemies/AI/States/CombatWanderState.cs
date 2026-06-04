using UnityEngine;
using UnityEngine.AI;

public class CombatWanderState : BaseState<EnemyContext>
{
    private Transform _transform;
    private NavMeshAgent _agent;
    private readonly EnemyCombatPlanner _planner;

    private const float LostVisualTTL = 2.0f;
    private const float EngageHoldDist = 3.0f;
    private const float RotationSpeed = 10f;

    private float _lostVisualTimer;
    private bool _rootLock;
    private float _rotationVelocity;

    public CombatWanderState(StateMachine fsm, EnemyContext ctx) : base(fsm, ctx)
    {
        _planner = _ctx.CombatPlanner;
    }

    private const float ENTER_EVALUATION_DELAY = 0.35f;

    public override void OnEnter()
    {
        _transform = _ctx.Owner.transform;
        _agent = _ctx.Agent;

        // Delay first planner evaluation so the Animator finishes transitioning into
        // the combat idle state before any attack CrossFade is issued. Without this,
        // Any-State transitions triggered by IsWandering=true override the attack
        // CrossFade and leave the attack runtime stuck active forever.
        _ctx.CombatPlanner.ScheduleRelease(ENTER_EVALUATION_DELAY);

        var eb = _ctx.Owner.GetComponent<EnemyBase>();
        if (eb != null) CombatAnalytics.EncounterStarted(eb, _transform.position);

        CombatMusicTracker.Instance?.NotifyEnterCombat();

        _lostVisualTimer = 0f;
        _rootLock = false;

        // Safety: clear any facing lock left by an action that was force-released
        // without calling OnInterrupted (e.g. interrupted mid-dodge by a hit).
        _ctx.Movement.LockFacing(false);

        _ctx.Perception.ForceMaxAlert();

        _ctx.Animation.Animator.SetBool(EnemyAnimHashes.HashIsWandering, true);
        _ctx.Movement.EnableMovement();
        _ctx.Movement.SetCombatWanderSpeed();

        _agent.updateRotation = false;
        _agent.updatePosition = true;
        _agent.isStopped = false;
    }

    public override void FixedTick(float fixedDt)
    {
        if (_ctx.BehaviorProfile.usesCombatPlanner)
        {
            _planner.TickPlan(fixedDt);
        }
    }

    public override void Tick(float dt)
    {
        float distToTarget = _ctx.Movement.DistanceToTarget();

        bool isAttacking = _planner.IsAttackRuntimeActive;

        if (distToTarget >= _ctx.CombatPlanner.combatRange && !isAttacking)
        {
            _finiteStateMachine.Set(_ctx.StateFactory.Chasing);
            return;
        }

        bool hasVisual = _ctx.Perception.HasVisual;

        if (hasVisual)
        {
            _lostVisualTimer = 0f;
        }
        else
        {
            _lostVisualTimer += dt;
        }

        bool keepEngaged = !hasVisual && (distToTarget <= EngageHoldDist || _lostVisualTimer <= LostVisualTTL);

        if (!hasVisual && !keepEngaged)
        {
            _planner.ForceRelease();

            Enums.AlertLevel level = _ctx.Perception.AlertLevel;

            if (level == Enums.AlertLevel.Alert || level == Enums.AlertLevel.Suspicious)
            {
                _finiteStateMachine.Set(_ctx.StateFactory.Alert);
            }
            else
            {
                _ctx.StateFactory.Investigate.Configure(activeSearch: true);
                _finiteStateMachine.Set(_ctx.StateFactory.Investigate);
            }

            return;
        }

        if (_ctx.Animation.IsAttacking)
        {
            if (!_rootLock) EnterRootLock();

            var currentAction = _planner.CurrentAction;
            if (currentAction != null)
            {
                Transform liveTarget = _ctx.Perception.CurrentTarget;
                float tNorm = _planner.AttackNormalizedTime;
                bool hardLook = currentAction.useHardLook ||
                    (_planner.ActiveAttack != null && _planner.ActiveAttack.IsHardLookWindowActive(tNorm));

                if (hardLook)
                {
                    if (liveTarget != null) FaceTowardsSmooth(liveTarget.position, RotationSpeed, dt);
                }
                else
                {
                    FaceTowardsSmooth(
                        _transform.position + _ctx.Movement.ForcedAttackDirection,
                        RotationSpeed, dt);
                }
            }

            _agent.nextPosition = _transform.position;
            return;
        }

        if (_rootLock) ExitRootLock();

        if (_ctx.Movement.IsFacingLocked) return;

        Transform target = _ctx.Perception.CurrentTarget;
        if (target != null) FaceTowardsSmooth(target.position, RotationSpeed, dt);
    }

    public override void OnExit()
    {
        base.OnExit();
        _ctx.Animation.Animator.SetBool(EnemyAnimHashes.HashIsWandering, false);
        ExitRootLock();
        _agent.updateRotation = true;
        _agent.updatePosition = true;
        _agent.autoBraking = true;

        CombatMusicTracker.Instance?.NotifyExitCombat();
    }

    #region Helpers

    private void FaceTowardsSmooth(Vector3 worldPos, float smoothSpeed, float dt)
    {
        Vector3 dir = worldPos - _transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        float targetAngle = targetRot.eulerAngles.y;
        float currentAngle = _transform.eulerAngles.y;

        float smoothedAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref _rotationVelocity, 1f / smoothSpeed, 360f, dt);

        _transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
    }

    private void EnterRootLock()
    {
        if (_rootLock)
        {
            _agent.nextPosition = _transform.position;
            return;
        }

        _rootLock = true;
        _agent.velocity = Vector3.Lerp(_agent.velocity, Vector3.zero, 0.5f);
        _agent.isStopped = true;
        _agent.updatePosition = false;
        _agent.ResetPath();
        _agent.nextPosition = _transform.position;
    }

    private void ExitRootLock()
    {
        if (!_rootLock) return;

        _rootLock = false;
        _agent.Warp(_transform.position);
        _agent.updatePosition = true;
        _agent.isStopped = false;
    }

    #endregion
}