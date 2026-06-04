using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
public class NearbyTracker : MonoBehaviour
{
    [Tooltip("Trigger radius (MUST BE the same as your lock radius).")] 
    public float radius = 18f;

    private readonly HashSet<Targetable> _inside = new();
    private readonly List<Targetable> _tempList = new();

    public bool HasEnemiesInside => _inside.Count > 0;

    private void Awake()
    {
        ApplyColliderSettings();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyColliderSettings();
    }
#endif

    private void Reset()
    {
        var sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = radius;
    }

    private void ApplyColliderSettings()
    {
        var sc = GetComponent<SphereCollider>();
        if (!sc) return;

        sc.isTrigger = true;
        sc.radius = radius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Targetable t))
            _inside.Add(t);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out Targetable t))
            _inside.Remove(t);
    }

    public void CopyTo(List<Targetable> dst)
    {
        dst.Clear();
        _tempList.Clear();

        // Single pass: collect valid and track invalid
        foreach (var targetable in _inside)
        {
            if (targetable && targetable.IsValid && targetable.IsAlive)
            {
                dst.Add(targetable);
            }
            else
            {
                _tempList.Add(targetable);
            }
        }

        // Clean up invalid entries
        if (_tempList.Count <= 0) return;
        
        foreach (var targetable in _tempList)
        {
            _inside.Remove(targetable);
        }
    }
}