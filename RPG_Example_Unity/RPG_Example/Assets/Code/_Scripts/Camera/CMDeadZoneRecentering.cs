using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public sealed class CMDeadZoneRecentering : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private PlayerInputs inputs;
    [SerializeField] private PlayerMovement movement;

    [Header("Idle Detection")]
    [Tooltip("Seconds of no input + low speed before recenter starts.")]
    [SerializeField] private float waitSeconds = 0.75f;

    [Tooltip("If planar speed is below this, we consider the character actually idle.")]
    [SerializeField] private float idleSpeedThreshold = 0.10f;

    [Header("Recentering")]
    [Tooltip("Seconds to shrink dead zone to near-zero (recentering).")]
    [SerializeField] private float recenterSeconds = 0.35f;

    [Tooltip("Seconds to restore dead zone when input resumes.")]
    [SerializeField] private float restoreSeconds = 0.15f;

    [Tooltip("Min dead zone size (normalized screen rect width/height). Keep small to avoid jitter.")]
    [SerializeField] private Vector2 minDeadZoneSize = new Vector2(0.01f, 0.01f);

    private CinemachinePositionComposer _composer;

    private Rect _baseDeadZoneRect;
    private float _idleTimer;
    private float _blend01; // 0 = normal deadzone, 1 = recentered (deadzone near min)

    private void Awake()
    {
        _composer = GetComponent<CinemachinePositionComposer>();
        if (_composer == null)
        {
            Debug.LogError("CMDeadZoneRecentering_PC requires CinemachinePositionComposer on the same GameObject.");
            enabled = false;
            return;
        }

        _baseDeadZoneRect = _composer.Composition.DeadZoneRect;
    }

    private void OnEnable()
    {
        _idleTimer = 0f;
        _blend01 = 0f;
        ApplyDeadZoneBlend(0f);
    }

    private void OnDisable()
    {
        // Restore original deadzone
        if (_composer == null) return;
        
        var comp = _composer.Composition;
        comp.DeadZoneRect = _baseDeadZoneRect;
        _composer.Composition = comp;
    }

    private void LateUpdate()
    {
        if (_composer == null || target == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool wantsMove = inputs != null && inputs.IsMoving();
        float planarSpeed = movement != null ? movement.CurrentPlanarSpeed : 0f;

        // If there is input OR we're still moving (physics/root motion), do NOT recenter.
        bool isActuallyIdle = !wantsMove && planarSpeed <= idleSpeedThreshold;

        if (isActuallyIdle)
        {
            _idleTimer += dt;

            if (_idleTimer >= waitSeconds)
            {
                float step = recenterSeconds <= 0.0001f ? 1f : (dt / recenterSeconds);
                _blend01 = Mathf.Clamp01(_blend01 + step);
            }
        }
        else
        {
            _idleTimer = 0f;

            float step = restoreSeconds <= 0.0001f ? 1f : (dt / restoreSeconds);
            _blend01 = Mathf.Clamp01(_blend01 - step);
        }

        ApplyDeadZoneBlend(_blend01);
    }

    private void ApplyDeadZoneBlend(float blend01)
    {
        // Keep the rect center, only shrink/restore size
        Rect rect = _baseDeadZoneRect;
        Vector2 baseSize = rect.size;

        float w = Mathf.Lerp(baseSize.x, minDeadZoneSize.x, blend01);
        float h = Mathf.Lerp(baseSize.y, minDeadZoneSize.y, blend01);

        Vector2 c = rect.center;
        rect.size = new Vector2(w, h);
        rect.center = c;

        var comp = _composer.Composition;
        comp.DeadZoneRect = rect;
        _composer.Composition = comp;
    }
}
