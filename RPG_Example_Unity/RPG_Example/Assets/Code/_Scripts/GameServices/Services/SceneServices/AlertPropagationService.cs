using UnityEngine;

[DisallowMultipleComponent]
public class AlertPropagationService : MonoBehaviour, IGameServices
{
    #region Fields

    [Header("Detection")]
    [Tooltip("Layer mask targeting the physical (non-trigger) colliders on enemy GameObjects. " +
             "Must NOT use the perception SphereCollider layer — that collider is a trigger " +
             "and OverlapSphereNonAlloc with Collide skips triggers by default in some Unity " +
             "versions. Point at the enemy body/ragdoll collider layer instead.")]
    [SerializeField]
    private LayerMask _enemyLayer;

    [Tooltip("Default radius used when the caller does not specify one.")] [SerializeField]
    private float _defaultRadius = 12f;

    [Tooltip("Max enemies checked per broadcast.")] [SerializeField, Min(4)]
    private int _overlapBufferSize = 32;

    private Collider[] _overlapBuffer;

    #endregion

    #region Properties

    public float DefaultPropagationRadius => _defaultRadius;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _overlapBuffer = new Collider[_overlapBufferSize];
    }

    #endregion

    #region Public API

    public void Broadcast(AlertEvent evt)
    {
        if (evt.Radius <= 0f || evt.AlertStrength <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(evt.Position, evt.Radius, _overlapBuffer, _enemyLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            var enemyBase = col.GetComponentInParent<EnemyBase>();
            if (enemyBase == null) continue;
            if (enemyBase == evt.Source) continue;

            var perception = enemyBase.Perception;
            if (perception == null) continue;

            float dist = Vector3.Distance(evt.Position, col.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / evt.Radius);
            float strength = evt.AlertStrength * falloff;

            if (strength <= 0f) continue;

            perception.ReceiveAlertPropagation(strength, evt.SourceAlertLevel);
        }
    }

    #endregion
}