using UnityEngine;

[DisallowMultipleComponent]
public class OcclusionTargetAnchor : MonoBehaviour
{
    [SerializeField] private Transform _samplePointOverride;

    public Vector3 GetWorldPoint() => _samplePointOverride != null ? _samplePointOverride.position : transform.position;

    private void OnEnable()
    {
        if (GameServices.TryGet<OcclusionService>(out var service))
        {
            service.RegisterTarget(this);
        }
    }

    private void OnDisable()
    {
        if (GameServices.TryGet<OcclusionService>(out var service))
        {
            service.UnregisterTarget(this);
        }
    }
}