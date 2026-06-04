using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(SphereCollider))]
public class EnemyPerception : MonoBehaviour
{
    #region Fields

    [Header("Utility Vision")]
    [Tooltip("Maximum distance at which the enemy can confirm direct vision (FOV + LOS). " +
             "Keep this <= SphereCollider radius. The collider can be larger to ensure " +
             "candidates never drop mid-chase, without increasing actual detection range.")]
    [SerializeField]
    private float _maxDetectionRange = 12f;

    [Tooltip("Within this radius the enemy detects the target regardless of FOV angle. Prevents losing sight at close range. Range: 2–4 m")] [SerializeField]
    private float closeCombatVisionDist = 3.0f;

    [Tooltip("Seconds HasVisual stays true after losing FOV/LOS. Prevents dropping the target on a single missed raycast. Range: 0.3–1.5 s")] [SerializeField]
    private float lostVisualGrace = 0.8f;

    [Tooltip("How strongly distance penalises farther targets in the utility score. Higher = always prefers the closest. Range: 0–1")] [SerializeField, Range(0f, 1f)]
    private float distanceWeight = 0.6f;

    [Tooltip("How strongly being directly ahead favours a target over one at the FOV edge. Range: 0–1")] [SerializeField, Range(0f, 1f)]
    private float angleWeight = 0.4f;

    [Tooltip("Score bonus subtracted from the current target to avoid rapid switching between close candidates. Higher = stickier. Range: 1000–5000")] [SerializeField]
    private float stickyBonus = 2500f;

    [Header("Priority (focuses target by priority)")]
    [Tooltip("Hostile factions in priority order (index 0 = highest). Factions not listed are ignored entirely.")]
    [SerializeField]
    private Enums.Faction[] _hostilePriority;

    [Header("Sensors")]
    [Tooltip("Controls FOV angle and LOS obstruction mask. Max detection range is controlled by _maxDetectionRange, NOT the SphereCollider radius.")]
    [SerializeField]
    private VisionSensor _visionSensor = new VisionSensor();

    [Tooltip("Detects noisy hostiles (sprinting, shooting) without needing LOS. Configure radius and threshold inside HearSensor.")] [SerializeField]
    private HearSensor _hearSensor = new HearSensor();

    [Header("Alert")] [Tooltip("Accumulates and decays alertValue to derive AlertLevel. Expand to tune detection speed, decay, and state thresholds.")] [SerializeField]
    private EnemyAlertController _alert = new EnemyAlertController();

    [SerializeField] private List<ITargetable> _candidates = new();
    private readonly HashSet<ITargetable> _candidateSet = new();
    private readonly Dictionary<Enums.Faction, int> _priorityMap = new();
    private readonly Dictionary<Collider, ITargetable> _colliderToTargetable = new();
    private readonly Dictionary<ITargetable, int> _colliderCountPerTarget = new();

    private EnemyContext _context;
    private SphereCollider _sphere;
    private EnemyBase _enemyBase;

    private float _lastSeenTime = -999f;
    private ITargetable _lastTargetable;

    private float _nextTickTime;
    private float _lastTickInterval;
    private bool _enabled = true;

    private bool _hasLastKnownPosition;

    private float _closeCombatVisionDist2;
    private float _maxDetectionRange2;
    private float _distWeightScaled;
    private float _angleWeightScaled;

    private const float PERIPHERAL_DOT_THRESHOLD = 0.5f;

    #endregion

    #region Properties

    public bool HasVisual { get; private set; }
    public bool HasAudio { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public Transform CurrentTarget { get; private set; }
    public ITargetable CurrentTargetableTarget { get; private set; }
    public float GetCloseVisionRadius => _sphere.radius;
    public IReadOnlyList<ITargetable> Candidates => _candidates;

    public Enums.AlertLevel AlertLevel => _alert.AlertLevel;
    public float AlertValue => _alert.AlertValue;
    public float SuspiciousThreshold => _alert.SuspiciousThreshold;
    public float CombatThreshold => _alert.CombatThreshold;

    public event Action<Enums.AlertLevel, Enums.AlertLevel> OnAlertLevelChanged;

    #endregion

    #region Initialization

    public void Initialize(EnemyContext ctx)
    {
        _context = ctx;
        _enabled = true;

        _alert.Reset();
        _hasLastKnownPosition = false;

        _lastTickInterval = _alert.TickInterval;
        _nextTickTime = Time.time + _lastTickInterval + Random.Range(0f, _lastTickInterval);

        CacheComputedValues();
        TryClearDeadCandidates();
        SyncMovementTarget(CurrentTarget);
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _sphere = GetComponent<SphereCollider>();
        _sphere.isTrigger = true;
        _enemyBase = GetComponentInParent<EnemyBase>();

        BuildPriorityMap();
        CacheComputedValues();
    }

    private void OnValidate()
    {
        BuildPriorityMap();
        CacheComputedValues();

#if UNITY_EDITOR
        var sphere = GetComponent<SphereCollider>();
        if (sphere != null && sphere.radius < _maxDetectionRange)
        {
            Debug.LogWarning(
                $"[EnemyPerception] {name}: SphereCollider radius ({sphere.radius}m) is smaller than " +
                $"maxDetectionRange ({_maxDetectionRange}m). The collider radius must be >= maxDetectionRange, " +
                $"ideally >= maxChaseDistance in EnemyBehaviorProfile, so candidates never drop mid-chase.",
                this);
        }
#endif
    }

    private void OnEnable()
    {
        _lastTickInterval = _alert.TickInterval;
        _nextTickTime = Time.time + _lastTickInterval + Random.Range(0f, _lastTickInterval);
    }

    private void OnDisable()
    {
        _colliderToTargetable.Clear();
        _colliderCountPerTarget.Clear();
    }

    private void Update()
    {
        if (!_enabled) return;
        if (Time.time < _nextTickTime) return;

        float dt = Time.time - (_nextTickTime - _lastTickInterval);

        _lastTickInterval = _alert.TickInterval;
        _nextTickTime = Time.time + _lastTickInterval;

        Evaluate(dt);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_enabled) return;

        if (!_colliderToTargetable.TryGetValue(other, out var targetable))
        {
            targetable = other.GetComponent<ITargetable>();
            if (targetable == null) return;

            _colliderToTargetable[other] = targetable;
        }

        if (targetable is not { IsAlive: true }) return;

        _colliderCountPerTarget.TryGetValue(targetable, out int count);
        _colliderCountPerTarget[targetable] = count + 1;

        if (_candidateSet.Add(targetable))
        {
            _candidates.Add(targetable);

            if (_enabled && !_hasLastKnownPosition)
            {
                float dt = _lastTickInterval;
                _lastTickInterval = _alert.TickInterval;
                _nextTickTime = Time.time + _lastTickInterval;
                Evaluate(dt);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Only process exits for colliders registered via TriggerEnter.
        if (!_colliderToTargetable.TryGetValue(other, out var targetable)) return;

        _colliderToTargetable.Remove(other);

        if (!_colliderCountPerTarget.TryGetValue(targetable, out int count)) return;

        count--;

        if (count > 0)
        {
            _colliderCountPerTarget[targetable] = count;
            return;
        }

        _colliderCountPerTarget.Remove(targetable);

        if (_candidateSet.Remove(targetable))
        {
            _candidates.Remove(targetable);
        }

        // Evaluate() is responsible for deciding when the target is truly lost.
    }

    #endregion

    #region Public API

    public bool HasLineOfSightTo(Vector3 mePos, Vector3 targetPos)
    {
        var to = targetPos - mePos;
        var d2 = to.sqrMagnitude;
        var dist = d2 > 0.000001f ? Mathf.Sqrt(d2) : 0f;
        return _visionSensor.HasLineOfSight(mePos, targetPos, dist);
    }

    public void EnablePerception()
    {
        _enabled = true;
        _lastTickInterval = _alert.TickInterval;
        _nextTickTime = Time.time + _lastTickInterval;
    }

    public void DisablePerception()
    {
        _enabled = false;
    }

    public void ClearLastTarget()
    {
        _lastTargetable = null;
        _lastSeenTime = -999f;
    }

    public void ReceiveAlertPropagation(float strength, Enums.AlertLevel sourceLevel)
    {
        Enums.AlertLevel before = _alert.AlertLevel;
        _alert.ReceivePropagation(strength, sourceLevel);
        NotifyLevelChange(before);
    }

    // Forces alert to maximum and notifies listeners — used when entering CombatWanderState.
    public void ForceMaxAlert()
    {
        Enums.AlertLevel before = _alert.AlertLevel;
        _alert.ForceMaxAlert();
        NotifyLevelChange(before);
    }

    public void ForceTarget(ITargetable target)
    {
        if (target == null) return;

        CurrentTarget = target.Transform;
        CurrentTargetableTarget = target;
        LastKnownPosition = target.Transform.position;
        _hasLastKnownPosition = true;
        _lastTargetable = target;
        _lastSeenTime = Time.time;
        HasVisual = true;

        SyncMovementTarget(CurrentTarget);
        ForceMaxAlert();
    }

    public void RestoreTarget(Transform targetTransform)
    {
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy) return;

        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].Transform == targetTransform)
            {
                ForceTarget(_candidates[i]);
                return;
            }
        }
    }

    #endregion

    #region Internal Logic

    private void Evaluate(float dt)
    {
        TryClearDeadCandidates();

        bool hasDirectVision = false;
        bool hasPeripheralVision = false;
        float distanceNorm01 = 0f;

        if (_lastTargetable is { IsAlive: true } && TryKeepCurrentTarget(out float dotToTarget, out distanceNorm01))
        {
            hasDirectVision = true;
            HasVisual = true;
        }
        else
        {
            var best = SelectBestByUtility(out dotToTarget, out distanceNorm01);

            if (best != null)
            {
                SetVariables(best);
                _lastTargetable = best;
                _lastSeenTime = Time.time;
                hasDirectVision = true;
            }
            else if (_lastTargetable != null)
            {
                var sinceSeen = Time.time - _lastSeenTime;
                var inGrace = sinceSeen <= lostVisualGrace;
                var d2 = (_lastTargetable.Transform.position - transform.position).sqrMagnitude;
                var inProximity = d2 <= _closeCombatVisionDist2;

                if (inGrace || inProximity)
                {
                    SetVariables(_lastTargetable);
                }
                else
                {
                    HasVisual = false;
                    CurrentTarget = null;
                    CurrentTargetableTarget = null;
                    SyncMovementTarget(null);
                }
            }
            else
            {
                HasVisual = false;
                CurrentTarget = null;
                CurrentTargetableTarget = null;
                SyncMovementTarget(null);
            }

            if (!hasDirectVision && _lastTargetable != null && _lastTargetable.IsAlive)
            {
                var eyePos = transform.position;
                Vector3 toTarget = _lastTargetable.Transform.position - eyePos;
                float d2 = toTarget.sqrMagnitude;

                if (d2 < _maxDetectionRange2)
                {
                    float dist = Mathf.Sqrt(d2);
                    float invDist = 1f / dist;
                    float dirX = toTarget.x * invDist;
                    float dirY = toTarget.y * invDist;
                    float dirZ = toTarget.z * invDist;

                    var forward = transform.forward;
                    float dot = forward.x * dirX + forward.y * dirY + forward.z * dirZ;

                    bool outsideMainFov = dot < _visionSensor.CosHalf;
                    bool inPeripheralArc = dot >= -PERIPHERAL_DOT_THRESHOLD;
                    bool hasLos = _visionSensor.HasLineOfSightDirect(eyePos, dirX, dirY, dirZ, dist);

                    if (outsideMainFov && inPeripheralArc && hasLos)
                    {
                        hasPeripheralVision = true;
                    }
                }
            }
        }

        bool hasAudioStimulus = false;
        if (_context.BehaviorProfile.reactsToSound)
        {
            var noisy = _hearSensor.GetBestNoisyHostile(transform.position, _candidates, _context.Targetable.Faction);
            if (noisy != null)
            {
                HasAudio = true;
                LastKnownPosition = noisy.Transform.position;
                _hasLastKnownPosition = true;
                hasAudioStimulus = true;
            }
            else
            {
                HasAudio = false;
            }
        }
        else
        {
            HasAudio = false;
        }

        Enums.AlertLevel before = _alert.AlertLevel;
        _alert.Tick(hasDirectVision, hasPeripheralVision, hasAudioStimulus, dt, distanceNorm01);
        NotifyLevelChange(before);
    }

    private bool TryKeepCurrentTarget(out float dotToTarget, out float distanceNorm01)
    {
        dotToTarget = 0f;
        distanceNorm01 = 0f;

        if (_lastTargetable == null) return false;
        if (!_priorityMap.ContainsKey(_lastTargetable.Faction)) return false;

        var eyePos = transform.position;
        var to = _lastTargetable.Transform.position - eyePos;
        float d2 = to.sqrMagnitude;
        if (d2 < 0.000001f) return false;

        if (d2 > _maxDetectionRange2) return false;

        float dist = Mathf.Sqrt(d2);
        float invDist = 1f / dist;

        float dirX = to.x * invDist;
        float dirY = to.y * invDist;
        float dirZ = to.z * invDist;

        var forward = transform.forward;
        dotToTarget = forward.x * dirX + forward.y * dirY + forward.z * dirZ;

        if (d2 > _closeCombatVisionDist2 && !_visionSensor.IsInFieldOfView(dotToTarget))
        {
            return false;
        }

        if (!_visionSensor.HasLineOfSightDirect(eyePos, dirX, dirY, dirZ, dist))
        {
            return false;
        }

        distanceNorm01 = Mathf.Clamp01(dist / _maxDetectionRange);
        SetVariables(_lastTargetable);
        _lastSeenTime = Time.time;
        return true;
    }

    private void SetVariables(ITargetable target)
    {
        HasVisual = true;
        HasAudio = false;
        CurrentTarget = target.Transform;
        CurrentTargetableTarget = target;
        LastKnownPosition = target.Transform.position;
        _hasLastKnownPosition = true;

        SyncMovementTarget(CurrentTarget);
    }

    private void SyncMovementTarget(Transform t)
    {
        if (_context == null || _context.Movement == null) return;
        if (_context.Movement.target == t) return;

        _context.Movement.target = t;
    }

    private void TryClearDeadCandidates()
    {
        for (int i = _candidates.Count - 1; i >= 0; --i)
        {
            var candidate = _candidates[i];
            if (candidate is { IsAlive: true }) continue;

            _candidateSet.Remove(candidate);
            _candidates.RemoveAt(i);

            if (_lastTargetable == candidate)
            {
                _lastTargetable = null;
                _lastSeenTime = -999f;
                HasVisual = false;
                CurrentTarget = null;
                CurrentTargetableTarget = null;
                SyncMovementTarget(null);
            }
        }
    }

    private ITargetable SelectBestByUtility(out float dotToBest, out float distanceNorm01)
    {
        dotToBest = 0f;
        distanceNorm01 = 0f;

        if (_candidates == null || _candidates.Count == 0) return null;

        ITargetable best = null;
        float bestScore = float.MaxValue;

        var eyePos = transform.position;
        var forward = transform.forward;
        float cosHalf = _visionSensor.CosHalf;

        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            var t = _candidates[i];
            if (t is not { IsAlive: true }) continue;
            if (!_priorityMap.TryGetValue(t.Faction, out int order)) continue;

            var to = t.Transform.position - eyePos;
            float d2 = to.sqrMagnitude;
            if (d2 < 0.000001f) continue;

            if (d2 > _maxDetectionRange2) continue;

            float dist = Mathf.Sqrt(d2);
            float invDist = 1f / dist;
            float dirX = to.x * invDist;
            float dirY = to.y * invDist;
            float dirZ = to.z * invDist;
            float dot = forward.x * dirX + forward.y * dirY + forward.z * dirZ;

            if (d2 > _closeCombatVisionDist2 && dot <= cosHalf) continue;

            float minPossibleUtility = order * 1_000_000f + dist * _distWeightScaled;

            if (_lastTargetable != null && t.Transform == _lastTargetable.Transform)
            {
                minPossibleUtility -= stickyBonus;
            }

            if (minPossibleUtility >= bestScore) continue;

            if (!_visionSensor.HasLineOfSightDirect(eyePos, dirX, dirY, dirZ, dist)) continue;

            float angleTerm01 = (1f - dot) * 0.5f;
            float utility = minPossibleUtility + angleTerm01 * _angleWeightScaled;

            if (!(utility < bestScore)) continue;

            bestScore = utility;
            best = t;
            dotToBest = dot;
            distanceNorm01 = Mathf.Clamp01(dist / _maxDetectionRange);
        }

        return best;
    }

    private void NotifyLevelChange(Enums.AlertLevel before)
    {
        Enums.AlertLevel after = _alert.AlertLevel;
        if (before == after) return;

        OnAlertLevelChanged?.Invoke(before, after);

        bool crossedAlertOrCombat = after >= Enums.AlertLevel.Alert && before < Enums.AlertLevel.Alert;

        if (!crossedAlertOrCombat) return;
        if (!GameServices.TryGet<AlertPropagationService>(out var propagation)) return;

        propagation.Broadcast(new AlertEvent
        {
            Position = transform.position,
            Radius = propagation.DefaultPropagationRadius,
            AlertStrength = 0.6f,
            Source = _enemyBase,
            SourceAlertLevel = after
        });
    }

    private void CacheComputedValues()
    {
        _closeCombatVisionDist2 = closeCombatVisionDist * closeCombatVisionDist;
        _maxDetectionRange2 = _maxDetectionRange * _maxDetectionRange;
        _distWeightScaled = distanceWeight * 1000f;
        _angleWeightScaled = angleWeight * 1000f;
    }

    private void BuildPriorityMap()
    {
        _priorityMap.Clear();
        if (_hostilePriority == null) return;

        for (int i = 0; i < _hostilePriority.Length; i++)
        {
            var f = _hostilePriority[i];
            if (!_priorityMap.ContainsKey(f))
            {
                _priorityMap.Add(f, i);
            }
        }
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_sphere == null) _sphere = GetComponent<SphereCollider>();

        float angle = _visionSensor != null ? _visionSensor.ViewAngle : 90f;

        // Detection range cone (direct vision).
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Vector3 forward = transform.forward;
        Vector3 rightLimit = Quaternion.Euler(0, angle * 0.5f, 0) * forward;
        Vector3 leftLimit = Quaternion.Euler(0, -angle * 0.5f, 0) * forward;
        Gizmos.DrawLine(transform.position, transform.position + rightLimit * _maxDetectionRange);
        Gizmos.DrawLine(transform.position, transform.position + leftLimit * _maxDetectionRange);

        // Collider radius (candidate tracking).
        float colliderRadius = _sphere != null ? _sphere.radius : 5f;
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, colliderRadius);
    }
#endif

    #endregion
}