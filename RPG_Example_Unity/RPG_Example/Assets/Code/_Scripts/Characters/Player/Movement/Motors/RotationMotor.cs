using UnityEngine;

public class RotationMotor
{
    private readonly Rigidbody _rb;

    public RotationMotor(Rigidbody rb)
    {
        _rb = rb;
    }

    public void RotateTowardsDirection(Vector3 dir, float speed, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR) return;
        
        var target = Quaternion.LookRotation(dir.normalized);
        var smooth = Quaternion.Slerp(_rb.rotation, target, 1f - Mathf.Exp(-speed * dt));

        if (Quaternion.Angle(_rb.rotation, smooth) > 0.01f)
        {
            _rb.MoveRotation(smooth);
        }
    }

    public void RotateTowardsTarget(Vector3 toTargetDir, float speed, float dt)
    {
        toTargetDir.y = 0f;
        if (toTargetDir.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR) return;

        var target = Quaternion.LookRotation(toTargetDir.normalized);
        var smooth = Quaternion.Slerp(_rb.rotation, target, 1f - Mathf.Exp(-speed * dt));

        if (Quaternion.Angle(_rb.rotation, smooth) > 0.01f)
        {
            _rb.MoveRotation(smooth);
        }
    }

    public void SnapToDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR) return;

        _rb.MoveRotation(Quaternion.LookRotation(dir.normalized));
    }
}