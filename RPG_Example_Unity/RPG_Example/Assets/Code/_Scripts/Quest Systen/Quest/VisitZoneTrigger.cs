using UnityEngine;

public class VisitZoneTrigger : MonoBehaviour
{
    [Tooltip("Must match VisitZoneStepSO.zoneId or DeliverItemStepSO.deliveryZoneId exactly.")]
    [SerializeField] private string _zoneId;

    [Tooltip("Tag used to identify the player collider.")]
    [SerializeField] private string _playerTag = "Player";

    [Tooltip("If true, trigger fires only once per scene load. Prevents re-reporting.")]
    [SerializeField] private bool _fireOnce = true;

    private bool _fired;

    private void OnTriggerEnter(Collider other)
    {
        if (_fireOnce && _fired) return;
        if (!other.CompareTag(_playerTag)) return;

        _fired = true;

        if (GameServices.TryGet<QuestService>(out var qs))
            qs.ReportZoneVisited(_zoneId);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Zone: {_zoneId}");
    }
#endif
}