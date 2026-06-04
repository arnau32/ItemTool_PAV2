using UnityEngine;

public sealed class CameraFollowProxyDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform proxy;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerInputs inputs;

    [Header("Plane")]
    [SerializeField] private bool lockYToPlayer = true;

    [Header("Idle Detection")]
    [SerializeField] private float idleSpeedThreshold = 0.08f;
    [SerializeField] private float waitSecondsBeforeRecentering = 0.75f;

    [Header("Follow Feel")]
    [Tooltip("Lower = snappier while moving.")]
    [SerializeField] private float smoothTimeMoving = 0.10f;

    [Tooltip("Lower = snappier when recentering after idle.")]
    [SerializeField] private float smoothTimeRecentering = 0.18f;

    [Header("Look Ahead")]
    [Tooltip("World units to lead at max speed.")]
    [SerializeField] private float maxLeadDistance = 1.2f;

    [Tooltip("How fast the lead offset reacts.")]
    [SerializeField] private float leadResponsiveness = 12f;

    [Tooltip("Clamp for extreme buffs. If <= 0, no clamp.")]
    [SerializeField] private float maxSpeedClamp = 0f;

    private Vector3 _proxyVel;
    private float _idleTimer;
    private Vector3 _leadOffset;
    private bool _initialized;

    private void Reset()
    {
        movement = GetComponent<PlayerMovement>();
        inputs = GetComponent<PlayerInputs>();
    }

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (inputs == null) inputs = GetComponent<PlayerInputs>();
    }

    private void LateUpdate()
    {
        if (proxy == null || movement == null) return;

        var rb = movement.Rigidbody;

        Vector3 playerPos = rb != null ? rb.transform.position : transform.position; // interpolated
        Vector3 planarVel = rb != null ? rb.linearVelocity : Vector3.zero;
        planarVel.y = 0f;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!_initialized)
        {
            proxy.position = playerPos;
            _proxyVel = Vector3.zero;
            _leadOffset = Vector3.zero;
            _idleTimer = 0f;
            _initialized = true;
        }

        float speed = planarVel.magnitude;

        bool wantsMove = inputs != null && inputs.IsMoving();
        bool isIdle = !wantsMove && speed <= idleSpeedThreshold;

        if (isIdle) _idleTimer += dt;
        else _idleTimer = 0f;

        bool recentering = isIdle && _idleTimer >= waitSecondsBeforeRecentering;

        // Direction: prefer input direction when there is input (more stable feel)
        Vector3 dir = Vector3.zero;
        if (wantsMove && inputs != null)
        {
            dir = inputs.GetDirectionNormalized();
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
        }
        else if (speed > 0.0001f)
        {
            dir = planarVel / speed;
        }

        // Dynamic "max speed" for lead normalization (supports stats + sprint)
        float speedForMaxLead = movement.IsSprinting ? movement.SprintSpeed : movement.RunSpeed;
        if (maxSpeedClamp > 0f) speedForMaxLead = Mathf.Min(speedForMaxLead, maxSpeedClamp);
        speedForMaxLead = Mathf.Max(0.01f, speedForMaxLead);

        float lead01 = Mathf.Clamp01(speed / speedForMaxLead);
        Vector3 targetLead = dir * (maxLeadDistance * lead01);

        // When recentering, fade lead to zero instead of hard snapping it
        float leadAlpha = 1f - Mathf.Exp(-leadResponsiveness * dt);
        if (recentering) targetLead = Vector3.zero;

        _leadOffset = Vector3.Lerp(_leadOffset, targetLead, leadAlpha);

        Vector3 targetPos = playerPos + _leadOffset;
        if (lockYToPlayer) targetPos.y = playerPos.y;

        float smoothTime = recentering ? smoothTimeRecentering : smoothTimeMoving;

        proxy.position = Vector3.SmoothDamp(
            proxy.position,
            targetPos,
            ref _proxyVel,
            Mathf.Max(0.0001f, smoothTime),
            Mathf.Infinity,
            dt
        );
    }
}
