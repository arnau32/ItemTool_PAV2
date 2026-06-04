using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    #region Fields

    public Transform target;

    [Header("Speeds")] [SerializeField] private float _runSpeed = 3f;
    [SerializeField] private float _walkSpeed = 1.5f;
    [SerializeField] private float _combatWanderSpeed = 1f;

    [Header("Smoothing")] [SerializeField] private float _speedSmoothTime = 0.20f;
    [SerializeField] private float _stopSmoothTime = 0.18f;

    [Header("Rotation")] [Tooltip("Slerp factor per second when rotating toward the steering target during a chase. Lower = more organic turn-in. Range: 3–8")] [SerializeField]
    private float _chaseRotationSpeed = 5f;

    [Header("Animation Smoothing")] [Tooltip("Damping time for the Speed animator parameter.")] [SerializeField]
    private float _animSpeedDampTime = 0.15f;

    [Header("Animation Drive")] [Tooltip("If true, drives DirX/DirY from navmesh velocity every frame (locked blendtree).")] [SerializeField]
    private bool _driveLockedBlendTree = true;

    [Tooltip("Use desiredVelocity when actual velocity is near zero.")] [SerializeField]
    private float _useDesiredVelThreshold = 0.06f;

    [Header("Attack Root Motion")] [Tooltip("Extra stop distance to prevent passing through the target during attack root motion.")] [SerializeField]
    private float _attackStopDistance = 0.15f;

    private static readonly float[] _sampleCos;
    private static readonly float[] _sampleSin;
    private const int FREE_SPACE_SAMPLES = 8;

    static EnemyMovement()
    {
        _sampleCos = new float[FREE_SPACE_SAMPLES];
        _sampleSin = new float[FREE_SPACE_SAMPLES];
        float step = 360f / FREE_SPACE_SAMPLES;
        for (int i = 0; i < FREE_SPACE_SAMPLES; i++)
        {
            float rad = step * i * Mathf.Deg2Rad;
            _sampleCos[i] = Mathf.Cos(rad);
            _sampleSin[i] = Mathf.Sin(rad);
        }
    }

    private static readonly Collider[] _separationBuffer = new Collider[8];

    private readonly float _switchDist = 0.8f;

    private EnemyContext _ctx;
    private NavMeshAgent _agent;

    private float _desiredSpeed;
    private float _currentSpeed;
    private float _speedVel;
    private bool _softStopping;

    private bool _hasSpeedOverride;
    private float _speedOverrideValue;
    private float _speedOverrideUntil;

    private EnemyCombatRootMotionMotor _rootMotionMotor;

    #endregion

    public Vector3 ForcedAttackDirection { get; set; }

    // When true, external code (e.g. DodgeAction) owns the rotation — states must not override it.
    public bool IsFacingLocked { get; private set; }

    public void LockFacing(bool v) => IsFacingLocked = v;

    public void EnableTurnMode(bool v)
    {
        _agent.updateRotation = !v;
    }

    #region Initialization

    public void Initialize(EnemyContext ctx)
    {
        _ctx = ctx;
        _agent = _ctx.Agent;
        SmoothToWalk();

        _rootMotionMotor = new EnemyCombatRootMotionMotor(_ctx, _attackStopDistance);
        _currentSpeed = 0f;
        _desiredSpeed = _walkSpeed;
        _hasSpeedOverride = false;
        _speedOverrideValue = 0f;
        _speedOverrideUntil = -1f;

        ApplySpeed(0f);
    }

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        if (!_agent.enabled) return;

        bool isHardAttack = _ctx.CombatPlanner.CurrentActionCategory == Enums.ActionCategory.Attack;
        if (_ctx.Animation.IsAttacking && isHardAttack) return;

        float desired = _desiredSpeed;

        if (_hasSpeedOverride)
        {
            if (Time.time < _speedOverrideUntil) desired = _speedOverrideValue;
            else _hasSpeedOverride = false;
        }

        float smoothTime = _softStopping ? _stopSmoothTime : _speedSmoothTime;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, desired, ref _speedVel, smoothTime);

        if (_softStopping && _currentSpeed < 0.05f)
        {
            _currentSpeed = 0f;
            _agent.isStopped = true;
            _softStopping = false;
        }

        ApplySpeed(_currentSpeed);

        float dt = Time.deltaTime;
        float animSpeed = _runSpeed > 0f ? _agent.velocity.magnitude / _runSpeed : 0f;
        _ctx.Animation.Animator.SetFloat(EnemyAnimHashes.HashSpeed, animSpeed, _animSpeedDampTime, dt);

        if (!_driveLockedBlendTree || _ctx.Animation is not EnemyAnimation enemyAnim) return;

        Vector3 v = _agent.velocity;
        v.y = 0f;

        if (v.magnitude < _useDesiredVelThreshold)
        {
            v = _agent.desiredVelocity;
            v.y = 0f;
        }

        enemyAnim.SetDirectionBlendTreeFromWorldVelocity(v, Mathf.Max(0.1f, _runSpeed), dt);
    }

    private void OnAnimatorMove()
    {
        if (_ctx?.Animation == null || _rootMotionMotor == null) return;

        // Non-attack root motion (e.g. dodge): agent position update is disabled externally.
        if (!_agent.updatePosition && !_ctx.Animation.IsAttacking)
        {
            Vector3 delta = _ctx.Animation.Animator.deltaPosition;
            if (delta.sqrMagnitude > 0.0000001f)
                transform.position += delta;
            _agent.nextPosition = transform.position;
            return;
        }

        if (!_ctx.Animation.IsAttacking) return;

        var attack = _ctx.CombatPlanner?.ActiveAttack;
        bool hasProceduralMove = attack != null && attack.UseProceduralForwardMove;
        bool hasRootDelta = _ctx.Animation.Animator.deltaPosition.sqrMagnitude > 0.0000001f;

        if (!hasRootDelta && !hasProceduralMove) return;

        _rootMotionMotor.ApplyAttackRootMotion();
    }

    #endregion

    #region Public API

    public bool HasFreeSpace(float radius = 1.2f)
    {
        if (_agent == null) return true;

        int blocked = 0;
        Vector3 origin = _ctx.Owner.transform.position;

        for (int i = 0; i < FREE_SPACE_SAMPLES; i++)
        {
            Vector3 p = new Vector3(
                origin.x + _sampleCos[i] * radius,
                origin.y,
                origin.z + _sampleSin[i] * radius);

            if (!NavMesh.SamplePosition(p, out _, 0.6f, NavMesh.AllAreas))
            {
                blocked++;
            }
        }

        return blocked < FREE_SPACE_SAMPLES * 0.4f;
    }

    // Prefer DistanceToTargetSqr for threshold comparisons — avoids Sqrt.
    public float DistanceToTarget()
    {
        return target == null ? 0f : Vector3.Distance(transform.position, target.position);
    }

    public float DistanceToTarget(Vector3 t) => Vector3.Distance(transform.position, t);

    public float DistanceToTargetSqr()
    {
        if (target == null) return 0f;
        Vector3 d = target.position - transform.position;
        return d.sqrMagnitude;
    }

    public float DistanceToTargetSqr(Vector3 t)
    {
        Vector3 d = t - transform.position;
        return d.sqrMagnitude;
    }

    public Vector3 DirToTarget() => (target.transform.position - transform.position).normalized;

    public void SmoothToRun()
    {
        _desiredSpeed = _runSpeed;
        _softStopping = false;
    }

    public void SmoothToWalk()
    {
        _desiredSpeed = _walkSpeed;
        _softStopping = false;
    }

    public void SmoothToCombat()
    {
        _desiredSpeed = _combatWanderSpeed;
        _softStopping = false;
    }

    // Aliases kept for caller compatibility.
    public void SetRunningSpeed() => SmoothToRun();
    public void SetWalkSpeed() => SmoothToWalk();
    public void SetCombatWanderSpeed() => SmoothToCombat();

    // Immediately sets agent speed to combatWanderSpeed with no smoothing.
    // Use when transitioning out of a run action (e.g. CloseGap arrival) so
    // the inspector value and the next action's path both start from the correct speed.
    public void SnapToCombatWanderSpeed()
    {
        _desiredSpeed  = _combatWanderSpeed;
        _currentSpeed  = _combatWanderSpeed;
        _speedVel      = 0f;
        _softStopping  = false;
        ApplySpeed(_currentSpeed);
    }

    public void SmoothStop()
    {
        _desiredSpeed = 0f;
        _softStopping = true;
    }

    public void HardStop()
    {
        _softStopping = false;
        _desiredSpeed = 0f;
        _currentSpeed = 0f;
        _speedVel = 0f;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
    }

    public void HandleChaseMovement()
    {
        if (target == null) return;

        SafeSetDestination(ComputeSeparatedDestination(target.position));

        RotateTowardSteering();
    }

    /// Predicts the player's future position and navigates there to cut them off.
    /// Called from ChasingState when this enemy has been assigned the interceptor role.
    public void HandleInterceptMovement(float predictionTime)
    {
        if (target == null) return;

        var targetable = _ctx.Perception.CurrentTargetableTarget;
        if (targetable == null)
        {
            HandleChaseMovement();
            return;
        }

        Vector3 vel = targetable.Velocity;
        vel.y = 0f;

        Vector3 predicted = target.position + vel * predictionTime;

        // If player is nearly stationary fall back to direct chase.
        if (vel.sqrMagnitude < 0.5f)
        {
            SafeSetDestination(ComputeSeparatedDestination(target.position));
        }
        else
        {
            // Aim slightly ahead of predicted position in movement direction.
            Vector3 dest = predicted + vel.normalized * 0.8f;
            SafeSetDestination(ComputeSeparatedDestination(dest));
        }

        RotateTowardSteering();
    }

    private void RotateTowardSteering()
    {
        if (_agent.updateRotation) return;

        Vector3 dir = (_agent.steeringTarget - transform.position).normalized;
        dir.y = 0f;

        if (!(dir.sqrMagnitude > 0.001f)) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            Time.deltaTime * _chaseRotationSpeed);
    }

    public void SetRootMotion(bool v)
    {
        _ctx.Animation.Animator.applyRootMotion = v;
        // Sync agent position before re-enabling updatePosition so it doesn't
        // snap the transform back from a stale internal position.
        if (!v)
            _agent.nextPosition = transform.position;
        _agent.updatePosition = !v;
        _agent.isStopped = v;
        if (v)
        {
            HardStop();
            _agent.ResetPath();
            _agent.nextPosition = transform.position;
        }
        else
        {
            // Snap back to the nearest NavMesh point in case root motion drifted
            // the transform off the mesh. Without this, _agent.nextPosition places
            // the agent off-mesh and all subsequent SafeSetDestination calls fail.
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            IsFacingLocked = false;
        }
    }

    public void EnableMovement()
    {
        EnableAgent();
        _agent.isStopped = false;
    }

    public void DisableMovement()
    {
        _agent.isStopped = true;
        _agent.enabled = false;
    }

    public void StartMovement() => _agent.isStopped = false;
    public void StopMovement() => SmoothStop();

    public void HandlePatrollArea()
    {
        if (!(_agent.remainingDistance <= _switchDist)) return;

        SafeSetDestination(UtilsNagu.GetRandomNavmeshPoint(transform.position, 10f));
    }

    public void HandlePatrollArea(Vector3 center, float radius)
    {
        if (!(_agent.remainingDistance <= _switchDist)) return;

        Vector3 next = UtilsNagu.GetRandomNavmeshPoint(center, radius);
        if (next == Vector3.zero) next = center;

        SafeSetDestination(next);
    }

    public void EnableAgent()
    {
        if (!_agent.enabled)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
            _agent.ResetPath();
        }
    }

    public void SafeSetDestination(Vector3 pos)
    {
        if (pos == Vector3.zero) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        _agent.SetDestination(pos);
    }

    public void HardLookAtTarget()
    {
        if (target == null) return;
        transform.LookAt(target);
    }

    public void PushSpeedOverride(float speed, float duration)
    {
        _hasSpeedOverride = true;
        _speedOverrideValue = Mathf.Max(0f, speed);
        _speedOverrideUntil = Time.time + Mathf.Max(0f, duration);
        _softStopping = false;
    }

    public void ClearSpeedOverride()
    {
        _hasSpeedOverride = false;
        _speedOverrideUntil = -1f;
    }

    #endregion

    #region Helpers

    /// Returns a chase destination offset laterally so this enemy approaches
    /// from a different angle than nearby allied enemies.
    public Vector3 ComputeSeparatedDestination(Vector3 targetPos)
    {
        if (_ctx?.BehaviorProfile == null) return targetPos;

        float sepRadius = _ctx.BehaviorProfile.separationRadius;
        if (sepRadius <= 0f || _ctx.BehaviorProfile.enemyLayer == 0) return targetPos;

        Vector3 selfPos = transform.position;
        Vector3 toTarget = targetPos - selfPos;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < 0.001f) return targetPos;

        Vector3 chaseDir = toTarget / dist;
        Vector3 right = new Vector3(-chaseDir.z, 0f, chaseDir.x);

        int count = Physics.OverlapSphereNonAlloc(
            selfPos, sepRadius, _separationBuffer,
            _ctx.BehaviorProfile.enemyLayer,
            QueryTriggerInteraction.Collide);

        // Accumulate a push perpendicular to the chase direction.
        // We decompose toOther into chaseDir + right components so that enemies
        // directly behind push laterally rather than producing zero dot product.
        float lateralPush = 0f;

        for (int i = 0; i < count; i++)
        {
            var col = _separationBuffer[i];
            if (col == null || col.transform == transform) continue;
            if (!col.CompareTag("Enemy")) continue;

            Vector3 toOther = selfPos - col.transform.position;
            toOther.y = 0f;
            float d = toOther.magnitude;
            if (d < 0.001f) continue;

            float overlap = Mathf.Clamp01(1f - d / sepRadius);

            // Project onto right axis — enemies alongside push sideways.
            float dot = Vector3.Dot(toOther.normalized, right);

            // If nearly collinear with chase dir (dot ≈ 0), add a deterministic
            // lateral bias based on instance ID so enemies split rather than stack.
            if (Mathf.Abs(dot) < 0.15f)
            {
                dot = (GetInstanceID() & 1) == 0 ? 0.3f : -0.3f;
            }

            lateralPush += dot * overlap;
        }

        if (Mathf.Abs(lateralPush) < 0.001f) return targetPos;

        // Apply offset to the self approach point, not to targetPos directly.
        // This spreads the approach angles rather than clustering around an offset target.
        float pushMag = Mathf.Clamp(lateralPush, -1f, 1f) * sepRadius * 0.8f;
        return targetPos + right * pushMag;
    }

    private void ApplySpeed(float v)
    {
        _agent.speed = v;
        if (v > 0.01f && _agent.isStopped)
        {
            _agent.isStopped = false;
        }
    }

    #endregion
}