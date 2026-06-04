using UnityEngine;

public class EnemyAnimation : CharacterAnimation
{
    [Header("Attack Speed")] 
    [SerializeField] private string _attackSpeedParameter = "AtkSpeed";

    [Header("Locked Blend Tree")] 
    [Tooltip("Damping time for DirX/DirY parameters to prevent snapping.")] [SerializeField] private float _directionDampTime = 0.12f;

    [Tooltip("Minimum planar speed to consider movement (avoid noisy tiny values).")] [SerializeField] private float _minPlanarSpeed = 0.03f;

    private int _attackSpeedHash;
    private float _lastAttackSpeed = 0f;

    protected override void Awake()
    {
        base.Awake();

        _attackSpeedHash = !string.IsNullOrEmpty(_attackSpeedParameter) ? Animator.StringToHash(_attackSpeedParameter) : 0;

        ResetAttackSpeed();
    }

    public void SetSpeed(float v)
    {
        _animator.SetFloat(EnemyAnimHashes.HashSpeed, v);
    }

    public void SetDirectionBlendTreeRaw(Vector3 dir)
    {
        _animator.SetFloat(EnemyAnimHashes.HashDirX, dir.x);
        _animator.SetFloat(EnemyAnimHashes.HashDirY, dir.z);
    }

    // Drive locked blendtree from actual world planar velocity with damping (no snapping)
    public void SetDirectionBlendTreeFromWorldVelocity(Vector3 worldPlanarVelocity, float referenceSpeed, float dt)
    {
        worldPlanarVelocity.y = 0f;

        if (dt <= 0f) dt = Time.deltaTime;

        // If almost not moving, blend towards 0 smoothly
        if (worldPlanarVelocity.magnitude < _minPlanarSpeed)
        {
            _animator.SetFloat(EnemyAnimHashes.HashDirX, 0f, _directionDampTime, dt);
            _animator.SetFloat(EnemyAnimHashes.HashDirY, 0f, _directionDampTime, dt);
            return;
        }

        var local = transform.InverseTransformDirection(worldPlanarVelocity);
        local.y = 0f;

        float denom = Mathf.Max(0.05f, referenceSpeed);
        float x = Mathf.Clamp(local.x / denom, -1f, 1f);
        float y = Mathf.Clamp(local.z / denom, -1f, 1f);

        _animator.SetFloat(EnemyAnimHashes.HashDirX, x, _directionDampTime, dt);
        _animator.SetFloat(EnemyAnimHashes.HashDirY, y, _directionDampTime, dt);
    }

    public void PlayHitAnimation(int num)
    {
        PlayTargetAnimation(EnemyAnimHashes.HashHit(num), 0f, EnemyAnimHashes.LayerUpwards);
    }

    public void SetAttackSpeedMultiplier(float multiplier)
    {
        if (_attackSpeedHash == 0) return;

        multiplier = Mathf.Max(0.01f, multiplier);

        if (Mathf.Abs(_lastAttackSpeed - multiplier) < 0.001f) return;

        _lastAttackSpeed = multiplier;
        _animator.SetFloat(_attackSpeedHash, multiplier);
    }

    public void ResetAttackSpeed() => SetAttackSpeedMultiplier(1f);

    public bool IsTurnAnimationDone()
    {
        bool inTransition = _animator.IsInTransition(EnemyAnimHashes.LayerOverride);
        var info = _animator.GetCurrentAnimatorStateInfo(EnemyAnimHashes.LayerOverride);
        bool isTurnState = info.shortNameHash == EnemyAnimHashes.HashTurnRight
                        || info.shortNameHash == EnemyAnimHashes.HashTurnLeft;
        if (isTurnState && !inTransition && info.normalizedTime >= 1f) return true;
        if (!isTurnState && !inTransition) return true;
        return false;
    }

    public void SetStopRotation()
    {
        _animator.SetTrigger(EnemyAnimHashes.HashStopRotation);
    }
}