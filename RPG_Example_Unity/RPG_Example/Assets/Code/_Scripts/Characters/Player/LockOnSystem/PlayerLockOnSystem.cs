using System.Collections.Generic;
using UnityEngine;

public class PlayerLockOnSystem : MonoBehaviour
{
    #region Fields

    [Header("References")] public Transform player;
    public Camera mainCam;
    public NearbyTracker tracker;

    [Header("Distances")] [Tooltip("Max distance to ACQUIRE a target.")]
    public float lockAcquireRadius = 18f;

    [Tooltip("Max distance to KEEP a locked target (>= acquire).")]
    public float lockMaintainRadius = 22f;

    [Header("Filters")] [Tooltip("Half angle of the frontal FOV cone (degrees).")] [Range(1f, 179f)]
    public float fovHalfAngle = 70f;

    [Tooltip("Layer mask for valid lock-on targets (enemies, etc).")]
    public LayerMask targetMask;

    [Tooltip("Layer mask for occluders that break line of sight.")]
    public LayerMask obstaclesMask;

    [Header("Timing")] [Tooltip("Anti-spam on changing. Minimum time between automatic or manual retargets.")]
    public float retargetCooldown = 0.20f;

    [Tooltip("Interval between occlusion checks to avoid spamming raycasts.")]
    public float occlusionCheckInterval = 0.20f;

    [Tooltip("Time to be able to swap target again")]
    public float timeToSwap = 0.25f;

    [Header("Behaviour")] [Range(0f, 0.5f)]
    public float hysteresis = 0.15f;

    [Tooltip("How long we tolerate losing LOS before dropping the lock.")]
    public float occlusionGrace = 0.35f;

    [Tooltip("If true, targets must be inside the viewport (camera) to be considered.")]
    public bool lockRequiresOnScreen = true;

    [Header("On Screen Drop")] [Tooltip("Drop lock if target is off-screen for longer than this time.")]
    public float offScreenGrace = 0.20f;

    [Header("Auto Lock")] [Tooltip("If enabled, when there is a target very close, lock-on will be acquired automatically.")]
    public bool autoLockEnabled = true;

    [Tooltip("Max distance to AUTO-ACQUIRE a target. Usually smaller than lockAcquireRadius.")]
    public float autoLockRadius = 7f;

    [Tooltip("Require clear line of sight for auto-lock.")]
    public bool autoLockRequiresLOS = true;

    [Tooltip("Require target to be camera-visible for auto-lock.")]
    public bool autoLockRequiresOnScreen = true;

    [Tooltip("After a manual unlock, auto-lock is suppressed for this time.")]
    public float autoLockSuppressAfterManualUnlock = 0.75f;

    [Header("Sprint Rotation Suspension")] [Tooltip("If true, sprinting (or holding sprint while moving) will suspend ONLY lock rotation (target stays locked).")]
    public bool sprintSuspendsLockRotation = true;

    [Header("Smart Lock")] [Tooltip("Minimum stick magnitude to consider direction input (0-1).")] [Range(0.1f, 0.9f)]
    public float rightStickDeadzone = 0.3f;

    [Header("Auto Aim")] [Tooltip("Enable soft auto-aim when attacking without lock-on.")]
    public bool autoAimEnabled = true;

    [Tooltip("Max angle (deg) between facing and target for auto-aim.")] [Range(1f, 180f)]
    public float autoAimMaxAngle = 55f;

    [Tooltip("Max distance for auto-aim to consider a target.")]
    public float autoAimMaxDistance = 12f;

    [Tooltip("Require clear line of sight for auto-aim.")]
    public bool autoAimRequiresLOS = true;

    [Tooltip("Require target to be camera-visible for auto-aim.")]
    public bool autoAimRequiresOnScreen = true;

    [Header("Mid-Attack Redirect")] [Tooltip("Max distance to consider a target for mid-attack redirection.")]
    public float attackRedirectMaxDistance = 6f;

    [Header("Line of Sight")] [Tooltip("Vertical offset from player root for the LOS raycast origin (eye / chest height).")]
    public float losEyeHeight = 1.5f;

    [Header("Non-Alloc Buffers")] [SerializeField, Min(1)]
    private int _overlapSize = 32;

    [SerializeField, Min(1)] private int _hitsSize = 16;
    [SerializeField, Min(1)] private int _candidatesSize = 32;

    // Runtime state
    private PlayerContext _ctx;
    private bool _isLockedOn;
    private bool _sprintRotationSuspended;
    private float _lastRetargetTime;
    private float _lastOcclusionCheck;
    private float _occludedSince = -1f;
    private float _offScreenSince = -1f;
    private float _autoLockSuppressedUntil = -1f;
    private float _swapTimer;
    private Targetable _softTarget;

    // Forward cache — recomputed once per frame
    private Vector3 _cachedForward;
    private int _cachedForwardFrame = -1;

    // LOS cache
    private readonly Dictionary<Targetable, CachedLOSEntry> _losCache = new();
    private readonly List<Targetable> _losCacheToRemove = new();
    private const float LOS_CACHE_INTERVAL = 0.1f;
    private const float LOS_ENTRY_LIFETIME = 0.3f;

    // Cached derived values — rebuilt whenever inspector fields change
    private float _lockAcquireRadiusSqr;
    private float _lockMaintainRadiusSqr;
    private float _autoLockRadiusSqr;
    private float _autoAimMaxDistanceSqr;
    private float _attackRedirectMaxDistSqr;
    private float _cosHalfFov;

    // Non-alloc physics buffers
    private List<Targetable> _candidates;
    private Collider[] _overlapResults;
    private RaycastHit[] _hits;
    private int _candidatesFrame = -1;

    private const float EPSILON_SQR = 0.0001f;
    private const float EPSILON = 0.01f;
    private const float DOT_TOLERANCE = 0.05f;

    #endregion

    #region Inner Types

    private struct CachedLOSEntry
    {
        public bool HasLOS;
        public float Timestamp;
    }

    #endregion

    #region Properties

    public bool IsLockedOn => _isLockedOn;
    public Targetable CurrentTarget { get; private set; }
    public bool IsLockRotationActive => _isLockedOn && !_sprintRotationSuspended;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        AllocateBuffers();
        RebuildCachedValues();
    }

    private void OnValidate()
    {
        _overlapSize = Mathf.Max(1, _overlapSize);
        _hitsSize = Mathf.Max(1, _hitsSize);
        _candidatesSize = Mathf.Max(1, _candidatesSize);
        RebuildCachedValues();
    }

    #endregion

    #region Public API

    public void Initialize(PlayerContext ctx)
    {
        _ctx = ctx;
        mainCam = Camera.main;
        player = _ctx.Owner.transform;
        AllocateBuffers();
        RebuildCachedValues();
    }

    public void Tick(float dt)
    {
        UpdateSprintSuspension();

        if (_ctx != null && _ctx.Animation != null)
        {
            _ctx.Animation.SetIsLocked(IsLockRotationActive);
        }

        bool isSprinting = _ctx != null && _ctx.Movement != null && _ctx.Movement.IsSprinting;
        bool sprintHeld = _ctx != null && _ctx.Movement != null && _ctx.Movement.IsSprintInputHeld;

        if (!_isLockedOn || CurrentTarget == null)
        {
            if (_isLockedOn) ClearLock(false);
            if (!isSprinting && !sprintHeld) TryAutoLock();
            return;
        }

        if (!CurrentTarget.IsValid || !CurrentTarget.IsAlive)
        {
            ClearLock(false);
            if (!isSprinting && !sprintHeld) TryAutoLock();
            return;
        }

        TickActiveLock();
    }

    public void ToggleLock()
    {
        if (_isLockedOn)
        {
            ClearLock(true);
            return;
        }

        AcquireSmartTarget();
    }

    public Vector3 GetDirectionToTargetNormalized(Vector3 fromPosition)
    {
        if (!_isLockedOn || CurrentTarget == null) return Vector3.zero;

        Vector3 dir = CurrentTarget.GetWorldAim() - fromPosition;
        dir.y = 0f;

        return dir.sqrMagnitude < EPSILON_SQR ? Vector3.zero : dir.normalized;
    }

    public void HandleDirectionalRightStickLockOn()
    {
        _swapTimer += Time.deltaTime;

        if (!_isLockedOn || _swapTimer < timeToSwap) return;

        Vector3 desiredDir = _ctx.Inputs.GetRightStickDirectionNormalized();
        if (desiredDir.sqrMagnitude < EPSILON) return;

        Targetable best = FindTargetInDirection(desiredDir);
        if (best == null || best == CurrentTarget) return;

        SetCurrent(best);
        _swapTimer = 0f;
    }

    // Used by combat to softly face a target while NOT locked-on.
    public bool TryGetAutoAimDirection(Vector3 origin, Vector3 facingDir, out Vector3 aimDir)
    {
        aimDir = Vector3.zero;

        if (!autoAimEnabled) return false;

        Targetable target = GetBestAutoAimTarget(origin, facingDir);
        if (!target) return false;

        Vector3 final = target.GetWorldAim() - origin;
        final.y = 0f;

        if (final.sqrMagnitude < EPSILON_SQR) return false;

        aimDir = final.normalized;
        return true;
    }

    // Used by CombatRootMotionMotor to clamp forward root motion distance.
    public Targetable GetCombatTarget(Vector3 origin, Vector3 facingDir)
    {
        if (_isLockedOn && CurrentTarget != null) return CurrentTarget;
        if (!autoAimEnabled) return null;

        if (TryKeepSoftTarget(origin)) return _softTarget;

        Targetable best = GetBestAutoAimTarget(origin, facingDir);
        _softTarget = best;
        return best;
    }

    public bool TrySwitchLockToDirection(Vector3 worldDir, out Vector3 dirToNewTarget)
    {
        dirToNewTarget = Vector3.zero;

        if (!_isLockedOn) return false;

        Targetable best = FindTargetInDirection(worldDir, _attackRedirectMaxDistSqr);
        if (best == null || best == CurrentTarget) return false;

        SetCurrent(best);

        Vector3 toNew = best.GetWorldAim() - player.position;
        toNew.y = 0f;

        if (toNew.sqrMagnitude < EPSILON_SQR) return false;

        dirToNewTarget = toNew.normalized;
        return true;
    }

    #endregion

    #region Tick Helpers

    private void UpdateSprintSuspension()
    {
        if (!sprintSuspendsLockRotation || !_isLockedOn || CurrentTarget == null)
        {
            _sprintRotationSuspended = false;
            return;
        }

        bool isSprinting = _ctx != null && _ctx.Movement != null && _ctx.Movement.IsSprinting;
        bool sprintHeld = _ctx != null && _ctx.Movement != null && _ctx.Movement.IsSprintInputHeld;
        bool wantsMove = _ctx != null && _ctx.Inputs != null && _ctx.Inputs.IsMoving();

        _sprintRotationSuspended = (isSprinting || sprintHeld) && wantsMove;
    }

    // Runs every Tick while a valid locked target exists.
    // Each check is isolated: returns early if the lock was dropped.
    private void TickActiveLock()
    {
        if (IsTargetOutOfRange()) return;
        if (IsTargetOffScreen()) return;

        float timeNow = Time.time;
        if (!(timeNow - _lastOcclusionCheck >= occlusionCheckInterval)) return;

        _lastOcclusionCheck = timeNow;
        TickTargetOcclusion(timeNow);
    }

    // Returns true and drops the lock when target exceeds the maintain radius.
    private bool IsTargetOutOfRange()
    {
        if ((player.position - CurrentTarget.GetWorldAim()).sqrMagnitude <= _lockMaintainRadiusSqr) return false;

        ClearLock(false);
        return true;
    }

    // Returns true and drops the lock when target has been off-screen beyond grace period.
    private bool IsTargetOffScreen()
    {
        if (!lockRequiresOnScreen) return false;

        float timeNow = Time.time;

        if (IsOnScreen(CurrentTarget))
        {
            _offScreenSince = -1f;
            return false;
        }

        if (_offScreenSince < 0f)
        {
            _offScreenSince = timeNow;
            return false;
        }

        if (timeNow - _offScreenSince <= offScreenGrace) return false;

        ClearLock(false);
        return true;
    }

    // Accumulates occluded time and drops the lock when beyond grace.
    private void TickTargetOcclusion(float timeNow)
    {
        if (HasLineOfSightCached(CurrentTarget))
        {
            _occludedSince = -1f;
            return;
        }

        if (_occludedSince < 0f)
        {
            _occludedSince = timeNow;
            return;
        }

        if (timeNow - _occludedSince > occlusionGrace)
        {
            ClearLock(false);
        }
    }

    #endregion

    #region Core Logic

    // Smart lock: prioritize best visible candidate, then right-stick direction, then closest.
    private void AcquireSmartTarget()
    {
        Targetable bestWithLOS = FindBestCandidateWithLOS();
        if (bestWithLOS != null)
        {
            SetCurrent(bestWithLOS);
            return;
        }

        Vector3 rightStick = _ctx.Inputs.GetRightStickDirectionNormalized();
        if (rightStick.sqrMagnitude >= rightStickDeadzone * rightStickDeadzone)
        {
            Targetable targetInDirection = FindTargetInDirection(rightStick);
            if (targetInDirection != null)
            {
                SetCurrent(targetInDirection);
                return;
            }
        }

        EnsureCandidates();
        if (_candidates.Count == 0) return;

        Targetable closest = null;
        float closestDistSqr = float.MaxValue;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Targetable t = _candidates[i];
            if (!IsCandidateValid(t)) continue;

            Vector3 to = t.GetWorldAim() - player.position;
            to.y = 0f;

            float d2 = to.sqrMagnitude;
            if (d2 > _lockAcquireRadiusSqr || d2 >= closestDistSqr) continue;

            closestDistSqr = d2;
            closest = t;
        }

        if (closest != null)
        {
            SetCurrent(closest);
        }
    }

    private void SetCurrent(Targetable t)
    {
        if (CurrentTarget != null)
        {
            UnbindCurrentTarget(CurrentTarget);
            CurrentTarget.SetLocked(false);
        }

        CurrentTarget = t;
        _isLockedOn = t != null;
        _lastRetargetTime = Time.time;
        _occludedSince = -1f;
        _offScreenSince = -1f;
        _swapTimer = 0f;
        _sprintRotationSuspended = false;

        if (CurrentTarget != null)
        {
            BindCurrentTarget(CurrentTarget);
            CurrentTarget.SetLocked(true);
        }

        _ctx.Animation.SetIsLocked(false);
    }

    private void ClearLock(bool manual)
    {
        if (CurrentTarget != null)
        {
            UnbindCurrentTarget(CurrentTarget);
            CurrentTarget.SetLocked(false);
        }

        CurrentTarget = null;
        _isLockedOn = false;
        _occludedSince = -1f;
        _offScreenSince = -1f;
        _swapTimer = 0f;
        _sprintRotationSuspended = false;

        if (manual)
        {
            _autoLockSuppressedUntil = Time.time + autoLockSuppressAfterManualUnlock;
        }

        _ctx.Animation.SetIsLocked(IsLockRotationActive);
    }

    private void TryAutoLock()
    {
        if (!autoLockEnabled) return;

        float now = Time.time;
        if (now < _autoLockSuppressedUntil) return;
        if (now - _lastRetargetTime < retargetCooldown) return;

        EnsureCandidates();
        if (_candidates.Count == 0) return;

        Vector3 fwd = GetForwardFlatCached();

        Targetable best = null;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Targetable t = _candidates[i];
            if (!IsCandidateValid(t)) continue;

            Vector3 aim = t.GetWorldAim();
            Vector3 to = aim - player.position;
            to.y = 0f;

            float d2 = to.sqrMagnitude;
            if (d2 < EPSILON_SQR || d2 > _autoLockRadiusSqr) continue;

            Vector3 dir = to / Mathf.Sqrt(d2);
            if (Vector3.Dot(fwd, dir) < _cosHalfFov) continue;

            if (autoLockRequiresOnScreen && !IsOnScreen(t)) continue;
            if (autoLockRequiresLOS && !HasLineOfSightCached(t)) continue;

            if (d2 >= bestD2) continue;

            best = t;
            bestD2 = d2;
        }

        if (best != null)
        {
            SetCurrent(best);
        }
    }

    private Targetable FindBestCandidateWithLOS()
    {
        EnsureCandidates();

        float bestScore = float.NegativeInfinity;
        Targetable best = null;

        Vector3 fwd = GetForwardFlatCached();

        for (int i = 0; i < _candidates.Count; i++)
        {
            Targetable t = _candidates[i];
            if (!IsCandidateValid(t)) continue;

            Vector3 aim = t.GetWorldAim();
            Vector3 to = aim - player.position;
            to.y = 0f;

            float d2 = to.sqrMagnitude;
            if (d2 < EPSILON_SQR) continue;

            Vector3 dir = to / Mathf.Sqrt(d2);
            if (Vector3.Dot(fwd, dir) < _cosHalfFov) continue;

            Vector3 vp = mainCam.WorldToViewportPoint(aim);
            if (vp.z <= 0f) continue;
            if (lockRequiresOnScreen && (vp.x <= 0f || vp.x >= 1f || vp.y <= 0f || vp.y >= 1f)) continue;

            if (!HasLineOfSightCached(t)) continue;

            float score = ComputeLockScore(fwd, dir, d2, vp) + t.StickinessBoost;
            if (t == CurrentTarget) score += hysteresis;

            if (score <= bestScore) continue;

            bestScore = score;
            best = t;
        }

        return best;
    }

    // Unified from FindTargetInDirectionWithinRange + FindTargetInWorldDirection.
    // maxDistSqr defaults to float.MaxValue (no distance cap) for the world-direction case.
    private Targetable FindTargetInDirection(Vector3 desiredDir, float maxDistSqr = float.MaxValue)
    {
        EnsureCandidates();

        desiredDir.y = 0f;
        float magSqr = desiredDir.sqrMagnitude;

        if (magSqr < EPSILON_SQR) return null;

        float invMag = 1f / Mathf.Sqrt(magSqr);
        desiredDir.x *= invMag;
        desiredDir.z *= invMag;

        float bestDot = -1f;
        float bestDist = float.MaxValue;
        Targetable best = null;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Targetable t = _candidates[i];
            if (!IsCandidateValid(t)) continue;
            if (lockRequiresOnScreen && !IsOnScreen(t)) continue;
            if (!HasLineOfSightCached(t)) continue;

            Vector3 to = t.GetWorldAim() - player.position;
            to.y = 0f;

            float d2 = to.sqrMagnitude;
            if (d2 < EPSILON_SQR || d2 > maxDistSqr) continue;

            float invDist = 1f / Mathf.Sqrt(d2);
            float dot = (desiredDir.x * to.x + desiredDir.z * to.z) * invDist;

            bool isBetter = dot > bestDot
                            || (Mathf.Abs(dot - bestDot) < DOT_TOLERANCE && d2 < bestDist);

            if (!isBetter) continue;

            bestDot = dot;
            bestDist = d2;
            best = t;
        }

        return best;
    }

    private Targetable GetBestAutoAimTarget(Vector3 origin, Vector3 facingDir)
    {
        EnsureCandidates();
        if (_candidates.Count == 0) return null;

        facingDir.y = 0f;
        float facingMagSqr = facingDir.sqrMagnitude;

        if (facingMagSqr < EPSILON_SQR)
        {
            facingDir = GetForwardFlatCached();
        }
        else
        {
            float invMag = 1f / Mathf.Sqrt(facingMagSqr);
            facingDir.x *= invMag;
            facingDir.y *= invMag;
            facingDir.z *= invMag;
        }

        float minDot = Mathf.Cos(autoAimMaxAngle * Mathf.Deg2Rad);

        Targetable best = null;
        float bestDot = minDot;
        float bestDist = float.MaxValue;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Targetable t = _candidates[i];
            if (!IsCandidateValid(t)) continue;

            Vector3 to = t.GetWorldAim() - origin;
            to.y = 0f;

            float d2 = to.sqrMagnitude;
            if (d2 < EPSILON_SQR || d2 > _autoAimMaxDistanceSqr) continue;

            float invDist = 1f / Mathf.Sqrt(d2);
            float dot = (facingDir.x * to.x + facingDir.z * to.z) * invDist;

            if (dot < minDot) continue;

            if (autoAimRequiresOnScreen && !IsOnScreen(t)) continue;
            if (autoAimRequiresLOS && !HasLineOfSightCached(t)) continue;

            bool isBetter = dot > bestDot
                            || (Mathf.Abs(dot - bestDot) < DOT_TOLERANCE && d2 < bestDist);

            if (!isBetter) continue;

            best = t;
            bestDot = dot;
            bestDist = d2;
        }

        return best;
    }

    // Returns true if _softTarget is still valid and reachable — avoids a full scan.
    private bool TryKeepSoftTarget(Vector3 origin)
    {
        if (_softTarget == null) return false;

        if (!_softTarget.IsValid || !_softTarget.IsAlive)
        {
            _softTarget = null;
            return false;
        }

        float keepDist = autoAimMaxDistance * 1.4f;
        float keepDistSqr = keepDist * keepDist;

        if ((_softTarget.GetWorldAim() - origin).sqrMagnitude > keepDistSqr)
        {
            _softTarget = null;
            return false;
        }

        if (autoAimRequiresOnScreen && !IsOnScreen(_softTarget))
        {
            _softTarget = null;
            return false;
        }

        if (autoAimRequiresLOS && !HasLineOfSightCached(_softTarget))
        {
            _softTarget = null;
            return false;
        }

        return true;
    }

    private float ComputeLockScore(Vector3 fwd, Vector3 dir, float d2, Vector3 vp)
    {
        float dx = vp.x - 0.5f;
        float dy = vp.y - 0.5f;
        float centerScore = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.6f);
        float distScore = 1f - Mathf.Clamp01(Mathf.Sqrt(d2) / lockAcquireRadius);
        float align = Mathf.Clamp01((Vector3.Dot(fwd, dir) + 1f) * 0.5f);

        return centerScore * 0.55f + distScore * 0.30f + align * 0.15f;
    }

    #endregion

    #region Target Binding

    private void BindCurrentTarget(Targetable t)
    {
        if (!t) return;

        t.OnInvalidated -= HandleTargetInvalidated;
        t.OnInvalidated += HandleTargetInvalidated;
    }

    private void UnbindCurrentTarget(Targetable t)
    {
        if (!t) return;

        t.OnInvalidated -= HandleTargetInvalidated;
    }

    private void HandleTargetInvalidated(Targetable t)
    {
        if (!t) return;

        if (t == CurrentTarget) ClearLock(false);
        if (t == _softTarget) _softTarget = null;
    }

    #endregion

    #region LOS

    private bool HasLineOfSightCached(Targetable t)
    {
        float now = Time.time;

        if (_losCache.TryGetValue(t, out CachedLOSEntry entry))
        {
            if (now - entry.Timestamp <= LOS_CACHE_INTERVAL) return entry.HasLOS;
        }

        if (_losCache.Count > 0 && _candidatesFrame % 30 == 0)
        {
            CleanupLOSCache(now);
        }

        bool los = HasWallOnlyLineOfSight(t);
        _losCache[t] = new CachedLOSEntry { HasLOS = los, Timestamp = now };
        return los;
    }

    private void CleanupLOSCache(float currentTime)
    {
        _losCacheToRemove.Clear();

        foreach (var kvp in _losCache)
        {
            if (currentTime - kvp.Value.Timestamp > LOS_ENTRY_LIFETIME)
            {
                _losCacheToRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < _losCacheToRemove.Count; i++)
        {
            _losCache.Remove(_losCacheToRemove[i]);
        }
    }

    private bool HasWallOnlyLineOfSight(Targetable t)
    {
        Vector3 from = player.position + Vector3.up * losEyeHeight;
        Vector3 toVec = t.GetWorldAim() - from;
        float distSqr = toVec.sqrMagnitude;

        if (distSqr < EPSILON_SQR) return true;

        float dist = Mathf.Sqrt(distSqr);
        Vector3 dir = toVec / dist;

        int hitCount = Physics.RaycastNonAlloc(from, dir, _hits, dist, obstaclesMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0) return true;

        Transform targetRoot = t.transform;
        int targetMaskValue = targetMask.value;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hits[i].collider;
            if (!col) continue;

            Transform hitTr = col.transform;

            if (hitTr == targetRoot || hitTr.IsChildOf(targetRoot)) continue;

            // Other enemies on the targetMask layer do not break lock-on.
            int layerBit = 1 << col.gameObject.layer;
            if ((targetMaskValue & layerBit) != 0) continue;

            return false;
        }

        return true;
    }

    #endregion

    #region Helpers

    private bool IsCandidateValid(Targetable t) => t != null && t.IsValid && t.IsAlive;

    private bool IsOnScreen(Targetable t)
    {
        Vector3 v = mainCam.WorldToViewportPoint(t.GetWorldAim());
        if (v.z <= 0f) return false;

        return v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    private void EnsureCandidates()
    {
        int f = Time.frameCount;
        if (_candidatesFrame == f) return;

        _candidatesFrame = f;
        _candidates.Clear();

        if (tracker)
        {
            tracker.CopyTo(_candidates);
            return;
        }

        int hits = Physics.OverlapSphereNonAlloc(
            player.position,
            lockAcquireRadius,
            _overlapResults,
            targetMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits; i++)
        {
            Targetable targetable = _overlapResults[i].GetComponent<Targetable>();
            if (targetable && IsCandidateValid(targetable))
            {
                _candidates.Add(targetable);
            }
        }
    }

    private Vector3 GetForwardFlatCached()
    {
        if (_cachedForwardFrame == Time.frameCount) return _cachedForward;

        _cachedForwardFrame = Time.frameCount;

        Vector3 fwd = player.forward;
        fwd.y = 0f;

        float magSqr = fwd.sqrMagnitude;

        if (magSqr < EPSILON_SQR)
        {
            _cachedForward = Vector3.forward;
        }
        else
        {
            float invMag = 1f / Mathf.Sqrt(magSqr);
            _cachedForward = new Vector3(fwd.x * invMag, 0f, fwd.z * invMag);
        }

        return _cachedForward;
    }

    private void AllocateBuffers()
    {
        _overlapResults = new Collider[_overlapSize];
        _hits = new RaycastHit[_hitsSize];

        if (_candidates == null)
        {
            _candidates = new List<Targetable>(_candidatesSize);
        }
        else
        {
            _candidates.Clear();
            if (_candidates.Capacity < _candidatesSize) _candidates.Capacity = _candidatesSize;
        }
    }

    // Rebuilds all values derived from serialized fields. Called from Awake and OnValidate.
    private void RebuildCachedValues()
    {
        _lockAcquireRadiusSqr = lockAcquireRadius * lockAcquireRadius;
        _lockMaintainRadiusSqr = lockMaintainRadius * lockMaintainRadius;
        _autoLockRadiusSqr = autoLockRadius * autoLockRadius;
        _autoAimMaxDistanceSqr = autoAimMaxDistance * autoAimMaxDistance;
        _attackRedirectMaxDistSqr = attackRedirectMaxDistance * attackRedirectMaxDistance;
        _cosHalfFov = Mathf.Cos(fovHalfAngle * Mathf.Deg2Rad);
    }

    #endregion
}