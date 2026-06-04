using UnityEngine;

public class LocomotionMotor
{
    private readonly Rigidbody _rb;
    private readonly GroundChecker _groundChecker;

    private readonly float _acceleration;
    private readonly float _deceleration;
    private readonly bool _useSlopePhysics;

    private readonly WallSlideDetector _wallSlideDetector;

    private Vector3 _planarVel;

    public LocomotionMotor(Rigidbody rb, GroundChecker groundChecker, float acceleration,
        float deceleration, bool useSlopePhysics, WallSlideDetector wallSlideDetector)
    {
        _rb = rb;
        _groundChecker = groundChecker;
        _acceleration = acceleration;
        _deceleration = deceleration;
        _useSlopePhysics = useSlopePhysics;
        _wallSlideDetector = wallSlideDetector;
    }

    public void TickHorizontal(float dt, Vector3 desiredDir, float runSpeed, float sprintSpeed, bool isSprinting, bool grounded)
    {
        var baseSpeed = isSprinting ? sprintSpeed : runSpeed;
        var throttle01 = Mathf.Clamp01(desiredDir.magnitude);
        var dir = Vector3.zero;
        
        if (throttle01 > 0f)
        {
            dir = desiredDir / throttle01;
            dir.y = 0f;
            if (dir.sqrMagnitude > UtilsNagu.EPSILON_DIR_SQR)
            {
                dir.Normalize();
            }
            else
            {
                dir = Vector3.zero;
            }
        }

        var targetSpeed = baseSpeed * throttle01;
        var targetPlanar = dir * targetSpeed;

        var linearVelocity = _rb.linearVelocity;
        _planarVel = new Vector3(linearVelocity.x, 0f, linearVelocity.z);

        var speedingUp = targetPlanar.sqrMagnitude > _planarVel.sqrMagnitude;
        var accel = speedingUp ? _acceleration : _deceleration;

        _planarVel = Vector3.MoveTowards(_planarVel, targetPlanar, accel * dt);

        if (_useSlopePhysics && grounded)
        {
            _planarVel = _groundChecker.ProjectOnGround(_planarVel);
        }

        if (_wallSlideDetector != null && _wallSlideDetector.Enabled)
        {
            _planarVel = _wallSlideDetector.ProjectPlanarVelocity(_planarVel, dt, _rb.position);
        }

        RigidbodyVelocityUtils.SetPlanarLinearVelocity(_rb, _planarVel, linearVelocity.y);
    }

    public float GetCurrentPlanarSpeed()
    {
        var linearVelocity = _rb.linearVelocity;
        var x = linearVelocity.x;
        var z = linearVelocity.z;
        return Mathf.Sqrt(x * x + z * z);
    }
}
