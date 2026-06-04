using System.Collections.Generic;
using UnityEngine;

public class OcclusionService : MonoBehaviour, IGameServices
{
    [Header("References")]
    [SerializeField] private Transform _cameraOrigin;
    [SerializeField] private OcclusionTargetAnchor _target;

    [Header("Auto Find")]
    [SerializeField] private bool _autoFindMainCamera = true;
    [SerializeField] private bool _autoFindTarget = true;

    [Header("Detection")]
    [SerializeField] private LayerMask _occludableMask;
    [SerializeField, Min(0.01f)] private float _sphereCastRadius = 0.65f;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Near Target Support")]
    [SerializeField] private bool _useNearTargetOverlap = true;
    [SerializeField, Min(0f)] private float _nearTargetRadius = 0.9f;

    [Header("Global Fade Defaults")]
    [SerializeField, Range(0f, 1f)] private float _defaultHiddenAlpha = 0f;
    [SerializeField, Min(0.01f)] private float _defaultFadeOutSpeed = 10f;
    [SerializeField, Min(0.01f)] private float _defaultFadeInSpeed = 6f;

    [Header("Buffers")]
    [SerializeField, Min(4)] private int _maxSphereHits = 32;
    [SerializeField, Min(4)] private int _maxOverlapHits = 32;

    [Header("Debug")]
    [SerializeField] private bool _drawDebug = false;

    private RaycastHit[] _sphereHits;
    private Collider[] _overlapHits;

    private readonly HashSet<OcclusionGroup> _currentLineOfSightGroups = new();
    private readonly HashSet<OcclusionGroup> _previousLineOfSightGroups = new();

    public float DefaultHiddenAlpha => _defaultHiddenAlpha;
    public float DefaultFadeOutSpeed => _defaultFadeOutSpeed;
    public float DefaultFadeInSpeed => _defaultFadeInSpeed;

    private void Awake()
    {
        _sphereHits = new RaycastHit[_maxSphereHits];
        _overlapHits = new Collider[_maxOverlapHits];
    }

    private void LateUpdate()
    {
        ResolveReferencesIfNeeded();

        if (_cameraOrigin == null || _target == null)
        {
            ClearPreviousLineOfSightGroups();
            return;
        }

        RefreshBuffersIfNeeded();
        _currentLineOfSightGroups.Clear();

        Vector3 origin = _cameraOrigin.position;
        Vector3 targetPoint = _target.GetWorldPoint();
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance > 0.001f)
        {
            direction /= distance;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _sphereCastRadius,
                direction,
                _sphereHits,
                distance,
                _occludableMask,
                _triggerInteraction
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _sphereHits[i].collider;
                if (hitCollider == null)
                    continue;

                RegisterGroupFromCollider(hitCollider);
            }
        }

        if (_useNearTargetOverlap && _nearTargetRadius > 0f)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(
                targetPoint,
                _nearTargetRadius,
                _overlapHits,
                _occludableMask,
                _triggerInteraction
            );

            for (int i = 0; i < overlapCount; i++)
            {
                Collider hitCollider = _overlapHits[i];
                if (hitCollider == null)
                    continue;

                RegisterGroupFromCollider(hitCollider);
            }
        }

        ApplyLineOfSightStateChanges();
        SwapSets();

        if (_drawDebug)
        {
            Debug.DrawLine(origin, targetPoint, Color.cyan);
        }
    }

    public void RegisterTarget(OcclusionTargetAnchor target)
    {
        if (target == null)
            return;

        _target = target;
    }

    public void UnregisterTarget(OcclusionTargetAnchor target)
    {
        if (_target == target)
            _target = null;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (_cameraOrigin == null && _autoFindMainCamera && Camera.main != null)
            _cameraOrigin = Camera.main.transform;

        if (_target == null && _autoFindTarget)
            _target = FindFirstObjectByType<OcclusionTargetAnchor>();
    }

    private void RegisterGroupFromCollider(Collider col)
    {
        OcclusionGroup group = col.GetComponentInParent<OcclusionGroup>();
        if (group == null)
            return;

        _currentLineOfSightGroups.Add(group);
    }

    private void ApplyLineOfSightStateChanges()
    {
        foreach (OcclusionGroup group in _currentLineOfSightGroups)
        {
            if (group == null)
                continue;

            group.SetReason(OcclusionReason.LineOfSight, true);
        }

        foreach (OcclusionGroup group in _previousLineOfSightGroups)
        {
            if (group == null)
                continue;

            if (!_currentLineOfSightGroups.Contains(group))
                group.SetReason(OcclusionReason.LineOfSight, false);
        }
    }

    private void SwapSets()
    {
        _previousLineOfSightGroups.Clear();

        foreach (OcclusionGroup group in _currentLineOfSightGroups)
            _previousLineOfSightGroups.Add(group);
    }

    private void ClearPreviousLineOfSightGroups()
    {
        foreach (OcclusionGroup group in _previousLineOfSightGroups)
        {
            if (group == null)
                continue;

            group.SetReason(OcclusionReason.LineOfSight, false);
        }

        _previousLineOfSightGroups.Clear();
        _currentLineOfSightGroups.Clear();
    }

    private void RefreshBuffersIfNeeded()
    {
        if (_sphereHits == null || _sphereHits.Length != _maxSphereHits)
            _sphereHits = new RaycastHit[_maxSphereHits];

        if (_overlapHits == null || _overlapHits.Length != _maxOverlapHits)
            _overlapHits = new Collider[_maxOverlapHits];
    }
}