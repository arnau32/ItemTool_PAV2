using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FloorZone : MonoBehaviour
{
    [SerializeField] private BuildingOcclusionController _building;
    [SerializeField] private int _floorIndex = 0;

    private readonly HashSet<OcclusionTargetAnchor> _targetsInside = new();

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (_building == null)
        {
            _building = GetComponentInParent<BuildingOcclusionController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        OcclusionTargetAnchor target = other.GetComponentInParent<OcclusionTargetAnchor>();
        
        if (target == null) return;

        if (!_targetsInside.Add(target)) return;

        if (_building != null)
        {
            _building.NotifyTargetEnteredFloor(_floorIndex);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        OcclusionTargetAnchor target = other.GetComponentInParent<OcclusionTargetAnchor>();
        
        if (target == null) return;

        if (!_targetsInside.Remove(target)) return;

        if (_building != null)
        {
            _building.NotifyTargetExitedFloor(_floorIndex);
        }
    }
}