using UnityEngine;

[System.Serializable]
public class PlayerLocomotion
{
    #region Fields

    [Header("Input smoothing")] [Tooltip("Intention seconds before not moving joystick")] [SerializeField]
    private float _intentCoyote = 0.12f;

    [SerializeField] private float _turnResponsiveness = 16f;
    [SerializeField] private float _hardTurnResponsiveness = 6f;
    [SerializeField] private float _throttleResponsiveness = 10f;

    [Tooltip("Has to be equal as the animator")]
    private const float WALK_THRESHOLD = 0.45f;

    private Vector3 _smoothedAim = Vector3.forward;
    private float _smoothedThrottle = 0f;
    private Vector3 _lastIntentDir = Vector3.forward;
    private float _lastIntentTime = -999f;

    private PlayerContext _ctx;
    private PlayerAnimation _animation;
    private PlayerMovement _movement;
    private PlayerInputs _inputs;

    private const float HARD_TURN_ANGLE_THRESHOLD = 100f;
    private const float WALK_SPRINT_BLEND_MAX = 1.5f;
    private const float EPSILON_SPEED = 0.01f;
    private const float ROTATION_EPSILON = 0.001f;
    private const float THROTTLE_EPSILON = 0.005f;

    #endregion

    #region Initialization

    public void Initialize(PlayerContext ctx)
    {
        _ctx = ctx;
        _movement = _ctx.Movement;
        _animation = ctx.Animation;
        _inputs = ctx.Inputs;

        _smoothedAim = Vector3.forward;
        _lastIntentDir = Vector3.forward;
        _smoothedThrottle = 0f;
        _lastIntentTime = -999f;
    }

    #endregion

    #region Update

    public void UpdateLocomotion(float dt)
    {
        if (_movement.DodgeSystem.IsInDodge && !_movement.DodgeSystem.MovementControlReleased) return;

        // Block all locomotion during knockback/knockdown — root motion drives movement.
        if (_ctx.PlayerLifeController != null && _ctx.PlayerLifeController.IsInHitReaction)
        {
            ZeroOutLocomotion(dt);
            return;
        }

        // Actions that lock movement (chest opening, dialogue) zero out locomotion entirely.
        if (_ctx.PlayableController != null && _ctx.PlayableController.BlocksMovement)
        {
            ZeroOutLocomotion(dt);
            return;
        }

        var now = Time.time;
        var (hasIntent, targetDir, targetThrottle) = ProcessInputIntent(now);

        _movement.SetHasMoveIntent(hasIntent);
        SmoothAimAndThrottle(hasIntent, targetDir, targetThrottle, dt);

        var desiredDirection = CalculateDesiredDirection();
        _movement.SetDesiredDirection(desiredDirection);

        UpdateAnimationParameters(desiredDirection, dt);
    }

    #endregion

    #region Input Processing

    private (bool hasIntent, Vector3 targetDir, float targetThrottle) ProcessInputIntent(float now)
    {
        var rawDirWs = _inputs.GetDirectionNormalized();
        var rawMag2 = rawDirWs.sqrMagnitude;

        // Use already computed magnitude instead of _inputs.HasRaw() (which recomputes direction again).
        var deadzone = _inputs.StickDeadZone;
        var deadzoneSqr = deadzone * deadzone;
        var hasRawInput = rawMag2 > deadzoneSqr;

        if (hasRawInput && rawMag2 > UtilsNagu.EPSILON_DIR_SQR)
        {
            _lastIntentDir = rawDirWs;
            _lastIntentTime = now;

            return (true, rawDirWs, Mathf.Clamp01(Mathf.Sqrt(rawMag2)));
        }

        // Intention coyote: maintain direction briefly
        return now - _lastIntentTime <= _intentCoyote ? (true, _lastIntentDir, 0f) : (false, Vector3.zero, 0f);
    }

    private Vector3 CalculateDesiredDirection() => _smoothedAim == Vector3.zero ? Vector3.zero : _smoothedAim * _smoothedThrottle;

    #endregion

    #region Animation Updates

    private void UpdateAnimationParameters(Vector3 desired, float dt)
    {
        UpdateDirectionalInput(desired, dt);
        UpdateSpeedParameter(dt);
    }

    private void UpdateDirectionalInput(Vector3 desired, float dt)
    {
        if (!ShouldUpdateDirectionalInput(desired))
        {
            _animation.SetInputValuesDamped(Vector2.zero, dt);
            return;
        }

        var localInput = TransformToLocalInput(desired);
        _animation.SetInputValuesDamped(localInput, dt);
    }

    private bool ShouldUpdateDirectionalInput(Vector3 desired) => _ctx.LockOnSystem.IsLockRotationActive && desired.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR;

    private Vector2 TransformToLocalInput(Vector3 desired)
    {
        Quaternion invRot = Quaternion.Inverse(_ctx.Transform.rotation);
        Vector3 local = invRot * desired;
        return new Vector2(local.x, local.z);
    }

    private void UpdateSpeedParameter(float dt)
    {
        var speedFromVelocity = ComputeSpeedParamFromVelocity();
        var speedFromStick = ComputeSpeedParamFromThrottle(_movement.IsSprinting, _smoothedThrottle);

        // Use max to avoid falling to idle when player turns fast
        var speedTarget = Mathf.Max(speedFromVelocity, speedFromStick);

        if (!_inputs.HasRaw() && !_movement.IsSprinting)
        {
            speedTarget = speedFromStick;
        }

        _animation.SetSpeedDamped(speedTarget, dt);
    }

    #endregion

    #region Speed Calculations

    // Uses Rigidbody velocity for animation stability during turns
    private float ComputeSpeedParamFromVelocity()
    {
        var v = _movement.CurrentPlanarSpeed;
        var run = _movement.RunSpeed;
        var sprint = _movement.SprintSpeed;

        if (!_movement.IsSprinting)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(0f, run, v));
        }

        // Sprint range: 1.0 to 1.5
        var sprintBlend = Mathf.InverseLerp(run, sprint, v);
        return Mathf.Clamp(1f + 0.5f * sprintBlend, 0f, WALK_SPRINT_BLEND_MAX);
    }

    // Uses stick throttle for immediate animation response
    private static float ComputeSpeedParamFromThrottle(bool isSprinting, float throttle01)
    {
        if (throttle01 <= 0f) return 0f;

        float baseSpeed = CalculateBaseSpeedFromThrottle(throttle01);

        if (!isSprinting) return baseSpeed;

        // Sprint adds extra blend: 0.0 -> 1.5
        return Mathf.Min(Mathf.Lerp(baseSpeed, WALK_SPRINT_BLEND_MAX, throttle01), WALK_SPRINT_BLEND_MAX);
    }

    private static float CalculateBaseSpeedFromThrottle(float throttle01)
    {
        if (throttle01 <= WALK_THRESHOLD)
        {
            // Walk range: 0.0 -> 0.5
            return Mathf.InverseLerp(0f, WALK_THRESHOLD, throttle01) * 0.5f;
        }

        // Run range: 0.5 -> 1.0
        return 0.5f + Mathf.InverseLerp(WALK_THRESHOLD, 1f, throttle01) * 0.5f;
    }

    #endregion

    #region Internal Logic

    private void SmoothAimAndThrottle(bool hasIntent, Vector3 targetDir, float targetThrottle, float dt)
    {
        float lerpTarget;

        if (hasIntent)
        {
            var aimTarget = targetDir.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR
                ? targetDir.normalized
                : _smoothedAim;

            // Skip rotation if already aligned (~2.5 degrees tolerance)
            if (Vector3.Dot(_smoothedAim, aimTarget) < 0.999f)
            {
                var turnSpeed = _turnResponsiveness;
                var angle = Vector3.Angle(_smoothedAim, aimTarget);

                if (angle > HARD_TURN_ANGLE_THRESHOLD)
                    turnSpeed *= _hardTurnResponsiveness;

                _smoothedAim = Vector3.Slerp(_smoothedAim, aimTarget, Mathf.Clamp01(turnSpeed * dt));
            }

            // Skip throttle lerp if negligible change
            float throttleDelta = Mathf.Abs(targetThrottle - _smoothedThrottle);
            if (!(throttleDelta > THROTTLE_EPSILON)) return;

            lerpTarget = Mathf.Clamp01(_throttleResponsiveness * dt);
            _smoothedThrottle = Mathf.Lerp(_smoothedThrottle, targetThrottle, lerpTarget);
        }
        else
        {
            // Lerp is asymptotic — snap to exactly 0 once below threshold so the
            // Animator param reaches 0 and the blend tree fully exits to idle.
            if (_smoothedThrottle <= THROTTLE_EPSILON)
            {
                if (_smoothedThrottle != 0f)
                {
                    _smoothedThrottle = 0f;
                    _smoothedAim = Vector3.zero;
                }

                return;
            }

            lerpTarget = Mathf.Clamp01(_throttleResponsiveness * dt);
            _smoothedThrottle = Mathf.Lerp(_smoothedThrottle, 0f, lerpTarget);

            // Snap once the lerp crosses the threshold
            if (_smoothedThrottle <= THROTTLE_EPSILON)
            {
                _smoothedThrottle = 0f;
                _smoothedAim = Vector3.zero;
            }
        }
    }

    // Sets desired direction and animation params to zero so the character
    // comes to a smooth stop during movement-blocking actions.
    private void ZeroOutLocomotion(float dt)
    {
        _movement.SetHasMoveIntent(false);
        _movement.SetDesiredDirection(Vector3.zero);
        _smoothedThrottle = 0f;
        _smoothedAim = Vector3.zero;
        _animation.SetInputValuesDamped(Vector2.zero, dt);
        _animation.SetSpeedDamped(0f, dt);
    }

    #endregion
}