using UnityEngine;

public class GroundMotor
{
    private readonly Rigidbody _rb;
    private readonly GroundChecker _groundChecker;

    private readonly float _gravity;
    private readonly float _snapLerp;
    private Vector3 _targetPosition;

    private float _smoothedGroundY;
    private bool _groundYInitialized;

    private const float GroundYSmoothSpeed = 30f;

    private const float GroundYMinChangeSqr = 0.0001f; // 1 cm threshold (0.01^2)

    private float _rawGroundYTarget;

    public bool IsGrounded => _groundChecker.IsGrounded();
    public bool IsStableGround => _groundChecker.IsStableGround;

    public float SmoothedGroundY => _smoothedGroundY;

    public GroundMotor(Rigidbody rb, GroundChecker groundChecker, float gravity, float snapLerp = 14f)
    {
        _rb = rb;
        _groundChecker = groundChecker;
        _gravity = gravity;
        _snapLerp = snapLerp;
        _groundYInitialized = false;
    }

    public void TickGround()
    {
        _groundChecker.CheckGround();
    }

    public void HandleVertical(float dt, bool allowSnapToGround, bool useSmoothSnap)
    {
        if (IsStableGround)
        {
            // Kill downward velocity — upward is kept so jump impulses are not cancelled.
            var linearVelocity = _rb.linearVelocity;
            if (linearVelocity.y < 0f)
            {
                linearVelocity.y = 0f;
                RigidbodyVelocityUtils.SetLinearVelocity(_rb, linearVelocity);
            }

            float rawGroundY = _groundChecker.HitInfo.point.y;

            if (!_groundYInitialized)
            {
                _smoothedGroundY = rawGroundY;
                _rawGroundYTarget = rawGroundY;
                _groundYInitialized = true;
            }
            else
            {
                float dy = rawGroundY - _rawGroundYTarget;
                if (dy * dy > GroundYMinChangeSqr)
                {
                    _rawGroundYTarget = rawGroundY;
                }

                _smoothedGroundY = Mathf.Lerp(_smoothedGroundY, _rawGroundYTarget, GroundYSmoothSpeed * dt);
            }

            if (!allowSnapToGround) return;

            _targetPosition = _rb.position;
            _targetPosition.y = _smoothedGroundY;

            if (useSmoothSnap)
            {
                _rb.MovePosition(Vector3.Lerp(_rb.position, _targetPosition, _snapLerp * dt));
            }
            else
            {
                _rb.MovePosition(_targetPosition);
            }
        }
        else
        {
            // Lost ground contact — reset smoothed Y so re-landing snaps correctly.
            _groundYInitialized = false;

            ApplyCustomGravity(dt);
        }
    }

    public void ResetGroundTracking()
    {
        _groundYInitialized = false;
        _rawGroundYTarget = 0f;
        _smoothedGroundY = 0f;
    }

    private void ApplyCustomGravity(float dt)
    {
        var gravityVector = Vector3.up * _gravity * dt;
        _rb.AddForce(gravityVector, ForceMode.Acceleration);
    }
}