using UnityEngine;

public interface ICharacterMovement
{
    void HandleAllMovement();
    void SetDesiredDirection(Vector3 dir);
    void SetSprint(bool isSprinting);
    void SetRootMotion(bool enabled);
    void SetLockOn(Transform target);
    bool IsGrounded();
}