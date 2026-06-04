using System;
using UnityEngine;

// Dodge System Managed by Animation Curve to be able to have better control over animation movement.
public class DodgeSystem
{
    #region Fields

    private PlayerContext _context;
    private Rigidbody _rigidbody;
    private GroundChecker _groundChecker;
    private WallSlideDetector _wallDetector;

    private DodgeDataSet _dodgeSet;
    private DodgeData _currentDodge;

    private float _dodgeTimer;
    private bool _isInDodge;
    private Vector3 _dodgeDirection;
    private float _normalizedTime01;

    [Header("Cardinal quantize")] private float _deadzone = 0.10f;
    private float _hysteresisDeg = 7.5f;
    private Vector2 _lastCardinal = Vector2.up;

    private Vector3 _lastPlanarVelocity;
    private float _postDodgeGraceDuration = 0.1f;
    private float _postDodgeGraceTimer;

    private bool _attackCancelOpen;
    private bool _attackBufferOpen;

    // Cancel-movement window state (does NOT end the dodge)
    private bool _moveCancelWindowOpen;
    private bool _movementControlReleased;

    private Action<string, float> _playAnim;

    #endregion

    #region Properties

    public Vector3 LastPlanarVelocity => _lastPlanarVelocity;
    public bool IsInPostDodgeGrace => _postDodgeGraceTimer > 0f;
    public bool IsInDodge => _isInDodge;
    public bool IsInIFrames => _isInDodge && _currentDodge != null && _currentDodge.iFrameWindow.IsActive(_dodgeTimer / _currentDodge.AnimationLength);
    public float DodgeNormalizedTime01 => _normalizedTime01;

    public bool MoveCancelOpen => _isInDodge && _currentDodge != null && _currentDodge.movementCancelation.IsActive(_normalizedTime01);

    public bool MoveCancelWindowOpen => _moveCancelWindowOpen;
    public bool MovementControlReleased => _movementControlReleased;

    public bool CanAttackCancel => _isInDodge && _attackCancelOpen;
    public bool CanBufferAttack => _isInDodge && _attackBufferOpen;

    public event Action OnDodgeEnded;

    #endregion

    #region Initialization

    public void Initialize(PlayerContext context, Rigidbody rb, GroundChecker gc, WallSlideDetector wallDetector)
    {
        _context = context;
        _rigidbody = rb;
        _groundChecker = gc;
        _wallDetector = wallDetector;

        _isInDodge = false;
        _dodgeTimer = 0f;
        _normalizedTime01 = 0f;
        _postDodgeGraceTimer = 0f;
        _attackCancelOpen = false;
        _attackBufferOpen = false;
        _currentDodge = null;
        _dodgeDirection = Vector3.zero;
        _lastPlanarVelocity = Vector3.zero;

        _moveCancelWindowOpen = false;
        _movementControlReleased = false;
        _playAnim = _context.Animation.PlayTargetAnimation;

        if (_context != null && _context.Animation != null)
            _context.Animation.ResetDodgeSpeed();
    }

    #endregion

    #region Public API

    public void TickGraceTimer(float dt)
    {
        if (_postDodgeGraceTimer > 0f)
            _postDodgeGraceTimer -= dt;
    }

    public bool TryExecuteDodge(bool isLockedOn, Vector3 worldDirection)
    {
        if (!CanExecuteDodge()) return false;
        ExecuteDodge(isLockedOn, worldDirection);
        return true;
    }

    public void SetDodgeSet(DodgeDataSet set) => _dodgeSet = set;

    public bool CanDodge()
    {
        if (_isInDodge) return false;
        return _dodgeSet != null && _context.Movement.IsGrounded();
    }

    public void ForceEndDodge()
    {
        if (!_isInDodge) return;

        Vector3 vel = _rigidbody.linearVelocity;
        vel.x = 0f;
        vel.z = 0f;
        _rigidbody.linearVelocity = vel;

        _lastPlanarVelocity = Vector3.zero;
        _postDodgeGraceTimer = 0f;

        _isInDodge = false;
        _attackCancelOpen = false;
        _attackBufferOpen = false;
        _currentDodge = null;
        _dodgeDirection = Vector3.zero;
        _normalizedTime01 = 0f;

        _moveCancelWindowOpen = false;
        _movementControlReleased = false;

        _context.Health.SetInvulnerable(false);

        if (_context != null && _context.Animation != null)
            _context.Animation.ResetDodgeSpeed();

        OnDodgeEnded?.Invoke();
    }

    #endregion

    #region Update

    public void UpdateDodge(float deltaTime)
    {
        if (!_isInDodge || _currentDodge == null) return;

        _dodgeTimer += deltaTime;

        var normalizedTime = Mathf.Clamp01(_dodgeTimer / _currentDodge.AnimationLength);
        _normalizedTime01 = normalizedTime;

        var shouldBeInvulnerable = _currentDodge.iFrameWindow.IsActive(normalizedTime);
        _context.Health.SetInvulnerable(shouldBeInvulnerable);

        _attackCancelOpen = _currentDodge.onBufferEndDodgeAnimation.IsActive(normalizedTime);
        _attackBufferOpen = _currentDodge.attackBufferWindow.IsActive(normalizedTime);

        _moveCancelWindowOpen = _currentDodge.movementCancelation.IsActive(normalizedTime);

        if (_moveCancelWindowOpen && _context.Inputs != null && _context.Inputs.IsMoving())
        {
            _movementControlReleased = true;
            _lastPlanarVelocity = Vector3.zero;
        }

        if (_attackCancelOpen && _context.CombatController.PostDodgeBuffer.HasInput)
        {
            ForceEndDodge();
            return;
        }

        ApplyDodgeAnimatorSpeed(normalizedTime);

        if (!_movementControlReleased)
            ApplyDodgeMovement(deltaTime, normalizedTime);

        if (normalizedTime >= 1f)
            EndDodge();
    }

    #endregion

    private void ApplyDodgeAnimatorSpeed(float normalizedTime)
    {
        if (_context == null || _context.Animation == null) return;

        if (_currentDodge == null || !_currentDodge.useAnimatorSpeedCurve)
        {
            _context.Animation.ResetDodgeSpeed();
            return;
        }

        var speed = _currentDodge.EvaluateAnimatorSpeed(normalizedTime);
        _context.Animation.SetDodgeSpeedMultiplier(speed);
    }

    private void ApplyDodgeMovement(float dt, float normalizedTime)
    {
        float currentSpeed = _currentDodge.EvaluateDodgeSpeed(normalizedTime);

        Vector3 planarVel = _dodgeDirection * currentSpeed;
        planarVel.y = 0f;

        bool grounded = _groundChecker.IsGrounded();

        if (grounded)
        {
            Vector3 planarDelta = planarVel * dt;
            Vector3 projectedDelta = _groundChecker.ProjectOnGround(planarDelta);

            Vector3 safeDelta = RigidbodySweepUtils.ClipDelta(_rigidbody, projectedDelta, _wallDetector);

            Vector3 nextPos = _rigidbody.position + safeDelta;

            float currentY = _rigidbody.position.y;
            float targetY = _groundChecker.GroundPoint.y + _dodgeSet.groundPointOffset;

            const float Y_EPS = 0.01f;
            const float Y_LERP = 0.35f;

            nextPos.y = Mathf.Abs(targetY - currentY) > Y_EPS
                ? Mathf.Lerp(currentY, targetY, Y_LERP)
                : currentY;

            _rigidbody.MovePosition(nextPos);

            _lastPlanarVelocity = dt > 0f ? safeDelta / dt : Vector3.zero;
        }
        else
        {
            Vector3 planarDelta = planarVel * dt;
            Vector3 safeDelta = RigidbodySweepUtils.ClipDelta(_rigidbody, planarDelta, _wallDetector);

            _rigidbody.MovePosition(_rigidbody.position + safeDelta);
            _lastPlanarVelocity = planarVel;
        }
    }

    private void ExecuteDodge(bool isLockedOn, Vector3 worldDirection)
    {
        _postDodgeGraceTimer = 0f;
        _normalizedTime01 = 0f;

        _moveCancelWindowOpen = false;
        _movementControlReleased = false;

        var (cardinal, dodgeDirWorld) = CalculateDodgeDirection(isLockedOn, worldDirection);

        _currentDodge = _dodgeSet.GetDodgeForDirection(isLockedOn, cardinal);
        _dodgeDirection = dodgeDirWorld;

        _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);

        _currentDodge.Execute(_playAnim);
        CombatAnalytics.PlayerDodged(isLockedOn, cardinal, _dodgeSet.staminaCost);

        // Dodge audio and VFX now live in PlayerFeedbacksController._feedbackDodge —
        // use FeedbackPlayAudio for the dodge SFX and FeedbackPlayAudioChance for the voice.
        _context.FeedbacksController?.PlayDodge();

        _isInDodge = true;
        _attackCancelOpen = false;
        _attackBufferOpen = false;
        _dodgeTimer = 0f;

        _lastPlanarVelocity = Vector3.zero;

        if (_context != null && _context.Animation != null)
            _context.Animation.ResetDodgeSpeed();
    }

    private (Vector2 cardinal, Vector3 worldDir) CalculateDodgeDirection(bool isLockedOn, Vector3 worldDirection)
    {
        return isLockedOn ? CalculateLockedOnDirection(worldDirection) : CalculateFreeDirection(worldDirection);
    }

    private (Vector2 cardinal, Vector3 worldDir) CalculateLockedOnDirection(Vector3 worldDirection)
    {
        var planar = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        var local = _context.Owner.transform.InverseTransformDirection(planar);
        Vector2 localXZ = new Vector2(local.x, local.z);

        var cardinal = QuantizeToCardinal(localXZ, _deadzone, _hysteresisDeg);
        if (cardinal == Vector2.zero) cardinal = Vector2.up;

        _context.Animation.SetInputValues(cardinal);

        var fwd = Vector3.ProjectOnPlane(_context.Owner.transform.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(_context.Owner.transform.right, Vector3.up).normalized;

        var dodgeDirWorld = right * cardinal.x + fwd * cardinal.y;

        if (dodgeDirWorld.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
            dodgeDirWorld = fwd;
        else
            dodgeDirWorld.Normalize();

        return (cardinal, dodgeDirWorld);
    }

    private (Vector2 cardinal, Vector3 worldDir) CalculateFreeDirection(Vector3 worldDirection)
    {
        var dodgeDirWorld = worldDirection;
        dodgeDirWorld.y = 0f;

        if (dodgeDirWorld.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
            dodgeDirWorld = _context.Owner.transform.forward;

        dodgeDirWorld.Normalize();
        _context.Movement.SnapFacing(dodgeDirWorld);

        return (Vector2.up, dodgeDirWorld);
    }

    private bool CanExecuteDodge()
    {
        if (_isInDodge) return false;
        if (_dodgeSet == null) return false;
        return _context.Stamina.TryConsumeStamina(_dodgeSet.staminaCost);
    }

    private void EndDodge()
    {
        if (!_movementControlReleased)
        {
            _rigidbody.linearVelocity = new Vector3(
                _lastPlanarVelocity.x,
                _rigidbody.linearVelocity.y,
                _lastPlanarVelocity.z
            );
        }

        _postDodgeGraceTimer = _postDodgeGraceDuration;

        _attackCancelOpen = false;
        _attackBufferOpen = false;
        _isInDodge = false;
        _context.Health.SetInvulnerable(false);
        _currentDodge = null;
        _dodgeDirection = Vector3.zero;
        _normalizedTime01 = 0f;

        _moveCancelWindowOpen = false;
        _movementControlReleased = false;
        _lastPlanarVelocity = Vector3.zero;

        if (_context != null && _context.Animation != null)
            _context.Animation.ResetDodgeSpeed();

        OnDodgeEnded?.Invoke();
    }

    #region Helpers

    private Vector2 QuantizeToCardinal(Vector2 v, float dead, float hystDeg)
    {
        var magnitude = v.magnitude;
        if (magnitude < dead) return Vector2.zero;

        var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        if (_lastCardinal != Vector2.zero && hystDeg > 0f)
        {
            if (IsInSector(angle, _lastCardinal, extra: hystDeg))
                return _lastCardinal;
        }

        _lastCardinal = angle switch
        {
            >= -45f and < 45f => Vector2.right,
            >= 45f and < 135f => Vector2.up,
            >= -135f and < -45f => Vector2.down,
            _ => Vector2.left
        };

        return _lastCardinal;
    }

    private bool IsInSector(float angDeg, Vector2 cardinal, float extra)
    {
        var center = cardinal.x > 0 ? 0f :
            cardinal.x < 0 ? 180f :
            cardinal.y > 0 ? 90f : -90f;

        var half = 45f + extra;
        var diff = Mathf.DeltaAngle(angDeg, center);
        return Mathf.Abs(diff) <= half;
    }

    #endregion
}