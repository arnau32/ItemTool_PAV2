using UnityEngine;

public class WallSlideDetector : MonoBehaviour
{
    [Header("Wall Slide")] [SerializeField]
    private bool _enabled = true;

    [SerializeField] private LayerMask _wallLM;
    [SerializeField] [Min(0.01f)] private float _castRadius = 0.28f;
    [SerializeField] [Min(0)] private float _castHeight = 1.0f;
    [SerializeField] [Min(0.001f)] private float _skin = 0.03f;
    [SerializeField] [Min(1)] private int _iterations = 2;

    [Header("Debug")] [SerializeField] private bool _drawGizmos = true;

    private Vector3 _dbgOrigin;
    private Vector3 _dbgDir;
    private float _dbgDist;
    private bool _dbgHadHit;
    private RaycastHit _dbgHit;

    public bool Enabled => _enabled;

    public LayerMask WallLayerMask => _wallLM;
    public float CastRadius => _castRadius;
    public float CastHeight => _castHeight;

    public Vector3 ProjectPlanarVelocity(Vector3 planarVel, float dt, Vector3 rbPosition)
    {
        if (!_enabled) return planarVel;
        if (planarVel.sqrMagnitude <= UtilsNagu.EPSILON_DIR_SQR) return planarVel;

        var origin = rbPosition + Vector3.up * _castHeight;

        var vel = planarVel;
        vel.y = 0f;

        _dbgOrigin = origin;
        _dbgHadHit = false;

        for (int i = 0; i < _iterations; i++)
        {
            var speed = vel.magnitude;
            if (speed < 0.0001f) return Vector3.zero;

            var dir = vel / speed;
            // Cast only as far as this frame's movement + skin — do NOT add skin to vel-only check,
            // otherwise a stationary player pressed against a wall keeps projecting every frame.
            var dist = speed * dt + _skin;

            _dbgDir = dir;
            _dbgDist = dist;

            if (!Physics.SphereCast(origin, _castRadius, dir, out var hit, dist, _wallLM, QueryTriggerInteraction.Ignore))
                return vel;

            _dbgHadHit = true;
            _dbgHit = hit;

            var into = Vector3.Dot(vel, hit.normal);

            // Only project if actually moving INTO the surface.
            // A positive dot means we're already moving away — no correction needed.
            if (into >= 0f) return vel;

            vel -= hit.normal * into;
            vel.y = 0f;

            // If projected velocity is negligible the player is cornered — stop cleanly.
            if (vel.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
                return Vector3.zero;
        }

        return vel;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos) return;

        Gizmos.DrawWireSphere(_dbgOrigin, _castRadius);

        if (_dbgDist > 0f)
        {
            Gizmos.DrawLine(_dbgOrigin, _dbgOrigin + _dbgDir * _dbgDist);
            Gizmos.DrawWireSphere(_dbgOrigin + _dbgDir * _dbgDist, _castRadius);
        }

        if (_dbgHadHit)
        {
            Gizmos.DrawWireSphere(_dbgHit.point, 0.06f);
            Gizmos.DrawLine(_dbgHit.point, _dbgHit.point + _dbgHit.normal * 0.5f);
        }
    }
}