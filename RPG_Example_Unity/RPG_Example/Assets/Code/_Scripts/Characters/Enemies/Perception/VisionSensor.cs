using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VisionSensor
{
    [Header("Sight Settings")] [SerializeField] [Range(0, 360)]
    private float _viewAngle = 90f;

    [SerializeField] private LayerMask _obstructionMask;

    private float _cachedCosHalf;
    private float _lastCachedAngle = float.NaN;

    public float ViewAngle => _viewAngle;

    public float CosHalf
    {
        get
        {
            if (_lastCachedAngle == _viewAngle) return _cachedCosHalf;
            _lastCachedAngle = _viewAngle;
            _cachedCosHalf = Mathf.Cos(Mathf.Deg2Rad * _viewAngle * 0.5f);
            return _cachedCosHalf;
        }
    }

    public bool TryGetBestByAngleAndLOS(Transform eye, Enums.Faction[] priority, List<ITargetable> candidates, float radius, out ITargetable best)
    {
        best = null;
        if (candidates == null || candidates.Count == 0) return false;

        var bestScore = float.MaxValue;
        var eyePos = eye.position;
        var eyeFwd = eye.forward;
        var cosHalf = CosHalf;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var target = candidates[i];

            var to = target.Transform.position - eyePos;
            var d2 = to.sqrMagnitude;
            if (d2 < 0.000001f) continue;

            float dist = Mathf.Sqrt(d2);
            float invDist = 1f / dist;

            float dirX = to.x * invDist;
            float dirY = to.y * invDist;
            float dirZ = to.z * invDist;

            // Inline FOV check
            float dot = eyeFwd.x * dirX + eyeFwd.y * dirY + eyeFwd.z * dirZ;
            if (dot <= cosHalf) continue;

            // Pass pre-computed direction — avoids recomputing inside HasLineOfSight
            if (!HasLineOfSightDirect(eyePos, dirX, dirY, dirZ, dist)) continue;

            int order = System.Array.IndexOf(priority, target.Faction);
            if (order < 0) continue;

            float score = order * 1_000_000f + d2;

            if (!(score < bestScore)) continue;

            bestScore = score;
            best = target;
        }

        return best != null;
    }

    public bool HasLineOfSightDirect(Vector3 from, float dirX, float dirY, float dirZ, float dist) =>
        !Physics.Raycast(from, new Vector3(dirX, dirY, dirZ), dist, _obstructionMask, QueryTriggerInteraction.Ignore);

    public bool HasLineOfSight(Vector3 from, Vector3 toPos, float dist)
    {
        var dir = toPos - from;

        // Manual normalize using known dist — avoids internal Sqrt from .normalized
        if (!(dist > 0.0001f)) return !Physics.Raycast(from, dir, dist, _obstructionMask, QueryTriggerInteraction.Ignore);

        float invDist = 1f / dist;
        dir.x *= invDist;
        dir.y *= invDist;
        dir.z *= invDist;

        return !Physics.Raycast(from, dir, dist, _obstructionMask, QueryTriggerInteraction.Ignore);
    }

    public bool IsInFieldOfView(float dot) => dot > CosHalf;

    // Public for other systems (EnemyPerception) to avoid Vector3.Angle/acos
    public bool IsInFieldOfView(Vector3 forward, Vector3 dirNormalized) => Vector3.Dot(forward, dirNormalized) > CosHalf;

    public bool IsVisibleWithProximity(Transform eye, Vector3 targetPos, float closeOverrideDist)
    {
        Vector3 to = targetPos - eye.position;
        float d2 = to.sqrMagnitude;
        if (d2 < 0.0001f) return true;

        float dist = Mathf.Sqrt(d2);
        float invDist = 1f / dist;

        float dirX = to.x * invDist;
        float dirY = to.y * invDist;
        float dirZ = to.z * invDist;

        bool inFov = dist <= closeOverrideDist || (eye.forward.x * dirX + eye.forward.y * dirY + eye.forward.z * dirZ) > CosHalf;
        if (!inFov) return false;

        return !Physics.Raycast(eye.position, new Vector3(dirX, dirY, dirZ), dist, _obstructionMask, QueryTriggerInteraction.Ignore);
    }
}