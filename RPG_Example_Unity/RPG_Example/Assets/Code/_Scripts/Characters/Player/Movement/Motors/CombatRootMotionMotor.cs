using UnityEngine;

public class CombatRootMotionMotor
{
    private readonly Rigidbody _rb;
    private readonly GroundChecker _groundChecker;
    private readonly GroundMotor _groundMotor;
    private readonly float _combatDistance;
    private readonly float _groundPointOffset;
    private readonly PlayerContext _context;
    private readonly PlayerCombatController _combatController;
    private readonly WallSlideDetector _wallDetector;

    // Procedural move state
    private ICombatMotionData _lastMotionData;
    private float _prevProc01;

    // Direction locked at attack start so rotation mid-attack doesn't cancel delta
    private Vector3 _lockedForward;

    public CombatRootMotionMotor(
        Rigidbody rb,
        GroundChecker groundChecker,
        GroundMotor groundMotor,
        float combatDistance,
        float groundPointOffset,
        PlayerContext context,
        PlayerCombatController combatController,
        WallSlideDetector wallDetector)
    {
        _rb = rb;
        _groundChecker = groundChecker;

        _groundMotor = groundMotor;

        _combatDistance = combatDistance;
        _groundPointOffset = groundPointOffset;
        _context = context;
        _combatController = combatController;

        _wallDetector = wallDetector;

        _lastMotionData = null;
        _prevProc01 = 0f;
        _lockedForward = Vector3.forward;
    }

    public void ApplyAttackRootMotion()
    {
        // Obtain current motion (attack or parry)
        ICombatMotionData motionData = GetCurrentMotionData(out float normalizedTime);

        if (motionData == null)
        {
            ResetProceduralState();
            return;
        }

        // Reset if motion has been changed — lock forward direction at attack start
        if (motionData != _lastMotionData)
        {
            _lastMotionData = motionData;
            _prevProc01 = 0f;
            _lockedForward = GetCurrentForward();
        }

        var delta = _context.Animation.Animator.deltaPosition;
        Quaternion nextRot = _rb.rotation * _context.Animation.Animator.deltaRotation;

        // Apply unified modifiers
        ApplyRootMotionMultiplier(motionData, normalizedTime, ref delta);
        ApplyProceduralForwardMove(motionData, normalizedTime, ref delta);
        ApplyCombatRootmotionClamp(ref delta);

        ApplyMovement(delta, nextRot);
    }

    private ICombatMotionData GetCurrentMotionData(out float normalizedTime)
    {
        var currentAttack = _context.ComboController.CurrentAttack;
        var currentParry = _combatController?.CurrentParryData;

        if (currentAttack != null)
        {
            normalizedTime = _context.ComboController.CurrentNormalizedTime;
            return currentAttack;
        }

        if (currentParry != null)
        {
            normalizedTime = _combatController.CurrentParryNormalizedTime;
            return currentParry;
        }

        normalizedTime = 0f;
        return null;
    }

    private void ResetProceduralState()
    {
        _lastMotionData = null;
        _prevProc01 = 0f;
        _lockedForward = Vector3.forward;
    }

    private void ApplyRootMotionMultiplier(ICombatMotionData motionData, float normalizedTime, ref Vector3 delta)
    {
        if (!motionData.UseRootMotionMultiplier) return;

        var multiplier = motionData.EvaluateRootMotionMultiplier(normalizedTime);
        delta.x *= multiplier;
        delta.z *= multiplier;
    }

    private void ApplyProceduralForwardMove(ICombatMotionData motionData, float normalizedTime, ref Vector3 delta)
    {
        if (!motionData.UseProceduralForwardMove) return;
        if (motionData.ProceduralForwardMoveDistance <= 0f) return;

        float proc01 = motionData.EvaluateProceduralForwardProgress01(normalizedTime);
        float d01 = Mathf.Max(0f, proc01 - _prevProc01);
        _prevProc01 = proc01;

        // Use current forward — procedural force follows the player's live rotation mid-attack
        Vector3 add = GetCurrentForward() * (d01 * motionData.ProceduralForwardMoveDistance);
        add.y = 0f;

        delta += add;
    }

    // Returns the current facing direction of the player, horizontal-only.
    private Vector3 GetCurrentForward()
    {
        Vector3 forward = _rb.rotation * Vector3.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
        {
            forward = _context.Owner.transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
            return Vector3.forward;

        forward.Normalize();
        return forward;
    }

    private void ApplyCombatRootmotionClamp(ref Vector3 delta)
    {
        Targetable combatTarget = _context.LockOnSystem.GetCombatTarget(_rb.position, _context.Owner.transform.forward);
        if (combatTarget == null) return;

        Vector3 toTarget = combatTarget.GetWorldAim() - _rb.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;

        Vector3 clampDir = dist < UtilsNagu.EPSILON_DIR_SQR
            ? _context.Owner.transform.forward
            : toTarget / dist;

        var forwardComponent = Vector3.Dot(delta, clampDir);
        if (!(forwardComponent > 0f)) return;

        // Always clamp regardless of current distance so lunges cannot overshoot combatDistance
        float maxForward = Mathf.Max(0f, dist - _combatDistance);
        if (forwardComponent > maxForward)
            delta -= clampDir * (forwardComponent - maxForward);
    }

    private void ApplyMovement(Vector3 delta, Quaternion nextRot)
    {
        Vector3 safeDelta = RigidbodySweepUtils.ClipDelta(_rb, delta, _wallDetector);

        if (_groundChecker.IsGrounded())
        {
            var groundNormal = _groundChecker.GroundNormal;
            safeDelta = Vector3.ProjectOnPlane(safeDelta, groundNormal);

            var newPos = _rb.position + new Vector3(safeDelta.x, 0f, safeDelta.z);

            newPos.y = _groundMotor.SmoothedGroundY + _groundPointOffset;

            _rb.MovePosition(newPos);
        }
        else
        {
            var planar = new Vector3(safeDelta.x, 0f, safeDelta.z);
            _rb.MovePosition(_rb.position + planar);
        }

        _rb.MoveRotation(nextRot);
    }
}
