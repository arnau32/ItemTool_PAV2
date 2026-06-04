using System;
using UnityEngine;

public class BuildingOcclusionController : MonoBehaviour
{
    [Serializable]
    public class FloorLevel
    {
        public string Name;
        public OcclusionGroup[] Groups;
    }

    [Header("Floors (asc)")]
    [SerializeField] private FloorLevel[] _floors;

    [Header("Roof Groups")]
    [SerializeField] private OcclusionGroup[] _roofGroups;

    [Header("Behaviour")]
    [SerializeField] private bool _hideRoofWhenInside = true;
    [SerializeField] private bool _hideFloorsAboveCurrent = true;
    [SerializeField] private bool _hideCurrentFloor = false;

    private int[] _floorOccupants;
    private int _currentFloor = -1;

    private void Awake()
    {
        _floorOccupants = new int[_floors != null ? _floors.Length : 0];
        ApplyVisibility();
    }

    public void NotifyTargetEnteredFloor(int floorIndex)
    {
        if (!IsValidFloorIndex(floorIndex)) return;

        _floorOccupants[floorIndex]++;
        RecalculateCurrentFloor();
    }

    public void NotifyTargetExitedFloor(int floorIndex)
    {
        if (!IsValidFloorIndex(floorIndex)) return;

        _floorOccupants[floorIndex] = Mathf.Max(0, _floorOccupants[floorIndex] - 1);
        RecalculateCurrentFloor();
    }

    private void RecalculateCurrentFloor()
    {
        int highestActiveFloor = -1;

        for (int i = 0; i < _floorOccupants.Length; i++)
        {
            if (_floorOccupants[i] > 0)
            {
                highestActiveFloor = i;
            }
        }

        _currentFloor = highestActiveFloor;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        bool isInside = _currentFloor >= 0;

        for (int i = 0; i < _floors.Length; i++)
        {
            bool shouldHide = false;

            if (isInside)
            {
                if (_hideCurrentFloor && i == _currentFloor)
                {
                    shouldHide = true;
                }
                else if (_hideFloorsAboveCurrent && i > _currentFloor)
                {
                    shouldHide = true;
                }
            }

            SetGroupsReason(_floors[i].Groups, OcclusionReason.Building, shouldHide);
        }

        if (_hideRoofWhenInside)
        {
            SetGroupsReason(_roofGroups, OcclusionReason.Building, isInside);
        }
        else
        {
            SetGroupsReason(_roofGroups, OcclusionReason.Building, false);
        }
    }

    private static void SetGroupsReason(OcclusionGroup[] groups, OcclusionReason reason, bool enabledState)
    {
        if (groups == null)
            return;

        foreach (var occlusionGroup in groups)
        {
            if (occlusionGroup == null) continue;

            occlusionGroup.SetReason(reason, enabledState);
        }
    }

    private bool IsValidFloorIndex(int floorIndex)
    {
        return floorIndex >= 0 && floorIndex < _floors.Length;
    }
}