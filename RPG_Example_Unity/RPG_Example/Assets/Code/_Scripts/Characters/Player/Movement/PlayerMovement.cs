using UnityEngine;

// Handles player movement, rotation, sprint, and dodge systems.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GroundChecker))]
public class PlayerMovement : MonoBehaviour
{
    #region Fields

    private DodgeSystem _dodgeSystem;

    [Header("Top-down Movement")] [SerializeField]
    private float _combatDistance = 1.5f;

    [SerializeField] private float _acceleration = 18f;
    [SerializeField] private float _deceleration = 22f;
    [SerializeField] private float _sprintBonus = 2.5f;
    [SerializeField] private float _gravity = -1000f;

    [Header("Rotation")] [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _attackRotationSpeed = 0f;

    [Header("Sprint")] [Tooltip("Min stamina required to start sprint when button is pressed")] [SerializeField]
    private float _minStaminaToStartSprint = 0.1f;

    [Tooltip("Min stamina required to keep sprint running. If below, sprint will stop and request will be cancelled")] [SerializeField]
    private float _minStaminaToKeepSprint = 0.01f;

    [Header("Options")] [Tooltip("True = slip on slopes")] [SerializeField]
    private bool _useSlopePhysics;

    [Header("Ground Adjust")] [SerializeField]
    private float _groundPointOffset;

    [SerializeField] private WallSlideDetector _wallSlideDetector;

    [Header("Mid-Attack Target Switch")] [Tooltip("Minimum angle (degrees) between joystick and current lock-on target to attempt switching.")] [SerializeField, Range(30f, 150f)]
    private float _attackRedirectMinAngle = 90f;

    private PlayerContext _context;
    private Rigidbody _rb;
    private GroundChecker _groundChecker;
    private PlayerCombatController _combatController;

    private GroundMotor _groundMotor;
    private LocomotionMotor _locomotionMotor;
    private RotationMotor _rotationMotor;
    private CombatRootMotionMotor _rootMotionMotor;
    private SprintHandler _sprintHandler;

    private Vector3 _desiredDir;
    private bool _hasMoveIntent;
    private bool _sprintButtonHeld;
    private float _attackRedirectMaxDot;

    private System.Func<bool> _isInCombatCached;

    #endregion

    #region Properties

    public float CurrentPlanarSpeed => _locomotionMotor.GetCurrentPlanarSpeed();
    public float RunSpeed => _context.Stats.GetStatValue(Enums.StatType.Speed);
    public float SprintSpeed => RunSpeed + _sprintBonus;
    public DodgeSystem DodgeSystem => _dodgeSystem;
    public bool IsSprinting => _sprintHandler.IsSprinting;
    public bool IsSprintInputHeld => _sprintButtonHeld;
    public bool IsGrounded() => _groundChecker.IsGrounded();
    public Rigidbody Rigidbody => _rb;

    #endregion

    #region Unity Callbacks

    private void OnAnimatorMove()
    {
        if (_context?.Animation == null) return;

        bool isAttacking = _context.Animation.IsAttacking;
        bool isParrying = _combatController?.IsParrying ?? false;
        bool isHitReaction = _context.PlayerLifeController?.IsInHitReaction ?? false;

        if (!isAttacking && !isParrying && !isHitReaction) return;

        bool wantsMove = _context.Inputs?.IsMoving() ?? false;

        if (isAttacking && _context.ComboController?.CanMoveDuringAttack() == true && wantsMove) return;
        if (isParrying && _combatController?.CanMoveCancelParry() == true && wantsMove) return;

        // Hit reactions always use root motion — no move cancel during knockback/knockdown.
        _rootMotionMotor.ApplyAttackRootMotion();
    }

    #endregion

    #region Public API

    public void Initialize(PlayerContext ctx)
    {
        _context = ctx;
        InitializeComponents();

        _groundMotor = new GroundMotor(_rb, _groundChecker, _gravity);
        _locomotionMotor = new LocomotionMotor(_rb, _groundChecker, _acceleration, _deceleration, _useSlopePhysics, _wallSlideDetector);
        _rotationMotor = new RotationMotor(_rb);
        _sprintHandler = new SprintHandler(_minStaminaToStartSprint, _minStaminaToKeepSprint);
        _rootMotionMotor = new CombatRootMotionMotor(_rb, _groundChecker, _groundMotor, _combatDistance, _groundPointOffset, ctx, _combatController, _wallSlideDetector);

        _dodgeSystem = new DodgeSystem();
        _dodgeSystem.Initialize(ctx, _rb, _groundChecker, _wallSlideDetector);

        _hasMoveIntent = false;
        _desiredDir = Vector3.zero;
        _sprintButtonHeld = false;
        _isInCombatCached = IsInCombat;
    }

    public void HandleAllMovement()
    {
        float dt = Time.fixedDeltaTime;

        _dodgeSystem.TickGraceTimer(dt);
        _groundMotor.TickGround();

        bool grounded = _groundMotor.IsGrounded;
        bool wantsMove = _context.Inputs?.IsMoving() ?? false;
        bool isAttacking = _context.Animation.IsAttacking;
        bool isParrying = _combatController?.IsParrying ?? false;

        bool dodgeActive = _dodgeSystem.IsInDodge;
        bool dodgeReleased = dodgeActive && _dodgeSystem.MovementControlReleased && wantsMove;

        bool inCombatAction = isAttacking || isParrying;
        bool combatMoveCancelReleased = (isAttacking && _context.ComboController?.CanMoveDuringAttack() == true && wantsMove)
                                        || (isParrying && _combatController?.CanMoveCancelParry() == true && wantsMove);

        bool allowSnapToGround = (!inCombatAction || combatMoveCancelReleased || dodgeReleased)
                                 && !dodgeActive && !_dodgeSystem.IsInPostDodgeGrace;

        bool useSmoothSnap = _hasMoveIntent && _desiredDir.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR;

        _groundMotor.HandleVertical(dt, allowSnapToGround, useSmoothSnap);

        if (dodgeActive)
        {
            _dodgeSystem.UpdateDodge(dt);
        }

        bool locomotionControlsPlanar = (!dodgeActive && !inCombatAction) || dodgeReleased || combatMoveCancelReleased;

        if (locomotionControlsPlanar)
        {
            _locomotionMotor.TickHorizontal(dt, _desiredDir, RunSpeed, SprintSpeed, _sprintHandler.IsSprinting, grounded);
        }
        else
        {
            RigidbodyVelocityUtils.SetPlanarLinearVelocity(_rb, Vector3.zero, _rb.linearVelocity.y);
        }

        if (_dodgeSystem.IsInDodge && !_dodgeSystem.MovementControlReleased) return;

        HandleRotation(dt);
    }

    public void SetDesiredDirection(Vector3 dir) => _desiredDir = dir.sqrMagnitude > 1f ? dir.normalized : dir;

    public void SnapFacing(Vector3 dir)
    {
        dir = UtilsNagu.Flatten(dir);
        if (!UtilsNagu.HasDirection(dir)) return;

        _rb.MoveRotation(Quaternion.LookRotation(dir.normalized));
    }

    public void SetHasMoveIntent(bool hasIntent) => _hasMoveIntent = hasIntent;

    public void HandleSprintInput(bool pressed)
    {
        _sprintButtonHeld = pressed;

        if (_context.LockOnSystem?.IsLockedOn == true)
        {
            _context.LockOnSystem.Tick(0f);
        }

        _sprintHandler.HandleInput(pressed, _context, CanSprintNow());
    }

    public void TickSprint()
    {
        if (_sprintButtonHeld && _context.LockOnSystem?.IsLockedOn == true)
        {
            _context.LockOnSystem.Tick(0f);
        }

        _sprintHandler.Tick(_context, CanSprintNow(), _isInCombatCached);
    }

    public void CancelSprintRequest() => _sprintHandler.CancelRequest(_context);
    public void SuspendSprintingKeepRequest() => _sprintHandler.SuspendSprintingKeepRequest(_context);

    public void Warp(Vector3 position)
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = position;
        _groundMotor.ResetGroundTracking();
    }

    public bool IsCancellingMovement(bool wantsMove)
    {
        var dodge = _dodgeSystem;

        if (dodge is { IsInDodge: true })
        {
            return dodge.MoveCancelWindowOpen && wantsMove;
        }

        if (_combatController != null && _combatController.IsParrying)
        {
            return _combatController.CanMoveCancelParry() && wantsMove;
        }

        return _context.ComboController.CanMoveDuringAttack() && wantsMove;
    }

    #endregion

    #region Helpers

    private void InitializeComponents()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_groundChecker == null) _groundChecker = GetComponent<GroundChecker>();
        if (_wallSlideDetector == null) _wallSlideDetector = GetComponent<WallSlideDetector>();
        if (_combatController == null) _combatController = GetComponent<PlayerCombatController>();

        _rb.useGravity = false;

        _attackRedirectMaxDot = Mathf.Cos(_attackRedirectMinAngle * Mathf.Deg2Rad);
    }

    private bool CanSprintNow()
    {
        if (_context == null) return false;
        if (_context.Animation.IsAttacking) return false;
        if (_combatController?.IsParrying == true) return false;
        return _hasMoveIntent;
    }

    // Combat is detected by:
    // 1. Having a locked-on target (most explicit combat state)
    // 2. Having enemies within combat detection radius (NearbyTracker)
    private bool IsInCombat()
    {
        if (_context?.LockOnSystem == null) return false;
        if (_context.LockOnSystem.IsLockedOn) return true;

        return _context.LockOnSystem.tracker?.HasEnemiesInside ?? false;
    }

    private void HandleRotation(float dt)
    {
        bool isAttacking = _context.Animation.IsAttacking;
        bool isParrying = _combatController?.IsParrying ?? false;
        bool inCombatAction = isAttacking || isParrying;

        bool canRotateInAction = !inCombatAction
                                 || (isAttacking && _context.ComboController?.CanRotateDuringAttack() == true)
                                 || (isParrying && _combatController?.CanRotateDuringParry() == true);

        float rotSpeed = inCombatAction ? _attackRotationSpeed : _rotationSpeed;
        if (!canRotateInAction) rotSpeed = 0f;

        bool inMoveRotation = isAttacking && _context.ComboController?.CanRotateDuringAttack() == true;

        if (_context.LockOnSystem is { IsLockedOn: true, IsLockRotationActive: true })
        {
            Vector3 dirToTarget = _context.LockOnSystem.GetDirectionToTargetNormalized(_rb.position);

            if (inMoveRotation && _desiredDir.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR
                               && dirToTarget.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR)
            {
                Vector3 stickNorm = _desiredDir.normalized;
                float dot = dirToTarget.x * stickNorm.x + dirToTarget.z * stickNorm.z;

                if (dot < _attackRedirectMaxDot)
                {
                    if (_context.LockOnSystem.TrySwitchLockToDirection(_desiredDir, out Vector3 dirToNew))
                    {
                        _rotationMotor.SnapToDirection(dirToNew);
                        return;
                    }
                }

                _rotationMotor.SnapToDirection(dirToTarget);
                return;
            }

            _rotationMotor.RotateTowardsTarget(dirToTarget, rotSpeed, dt);
        }
        else
        {
            if (inMoveRotation && _desiredDir.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR)
            {
                _rotationMotor.SnapToDirection(_desiredDir);
                return;
            }

            _rotationMotor.RotateTowardsDirection(_desiredDir, rotSpeed, dt);
        }
    }

    #endregion
}