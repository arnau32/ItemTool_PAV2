using UnityEngine;

public class PlayerAnimation : CharacterAnimation
{
    [Header("Animation Damping")] [SerializeField]
    private float _inputDamp = 0.12f;

    [SerializeField] private float _speedDamp = 0.10f;

    private const float SpeedSnapThreshold = 0.015f;

    private float _lastAttackSpeed = 0f;
    private float _lastDodgeSpeed = 0f;
    private bool _lastLocked;

    #region Public API

    protected override void Awake()
    {
        base.Awake();
        SetAttackSpeedMultiplier(1f);
    }

    public void SetIsLocked(bool b)
    {
        if (_lastLocked == b) return;
        _lastLocked = b;
        _animator.SetBool(PlayerAnimHashes.IsLockedBool, b);
    }

    // Call this after swapping runtimeAnimatorController — the new controller resets all params.
    public void OnAnimatorControllerChanged()
    {
        _animator.SetBool(PlayerAnimHashes.IsLockedBool, _lastLocked);
    }

    public void SetSpeedDamped(float speedParam, float dt)
    {
        if (speedParam < SpeedSnapThreshold)
        {
            _animator.SetFloat(PlayerAnimHashes.HashSpeed, 0f);
            return;
        }

        _animator.SetFloat(PlayerAnimHashes.HashSpeed, speedParam, _speedDamp, dt);
    }

    public void SetInputValuesDamped(Vector2 inputMovement, float dt)
    {
        _animator.SetFloat(PlayerAnimHashes.HashDirX, inputMovement.x, _inputDamp, dt);
        _animator.SetFloat(PlayerAnimHashes.HashDirY, inputMovement.y, _inputDamp, dt);
    }

    public void SetInputValues(Vector2 inputMovement)
    {
        _animator.SetFloat(PlayerAnimHashes.HashDirX, inputMovement.x);
        _animator.SetFloat(PlayerAnimHashes.HashDirY, inputMovement.y);
    }

    public void SetAttackSpeedMultiplier(float multiplier)
    {
        multiplier = Mathf.Max(0.01f, multiplier);

        if (Mathf.Abs(_lastAttackSpeed - multiplier) < 0.001f) return;

        _lastAttackSpeed = multiplier;
        _animator.SetFloat(PlayerAnimHashes.HashAttackSpeedParameter, multiplier);
    }

    public void ResetAttackSpeed() => SetAttackSpeedMultiplier(1f);

    public void SetDodgeSpeedMultiplier(float multiplier)
    {
        multiplier = Mathf.Max(0.01f, multiplier);

        if (Mathf.Abs(_lastDodgeSpeed - multiplier) < 0.001f) return;

        _lastDodgeSpeed = multiplier;
        _animator.SetFloat(PlayerAnimHashes.HashDodgeSpeedParameter, multiplier);
    }

    public void ResetDodgeSpeed() => SetDodgeSpeedMultiplier(1f);

    #endregion
}