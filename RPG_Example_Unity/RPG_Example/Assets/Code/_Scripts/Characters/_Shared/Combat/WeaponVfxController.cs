using System;
using UnityEngine;
using UnityEngine.Serialization;

public class WeaponVfxController : MonoBehaviour
{
    [Serializable]
    public struct VfxGroup
    {
        [Tooltip("Particle GameObjects controlled by this id.")] public GameObject[] vfxObjects;

        [Tooltip("If true, Clear() is called right before enabling emission.")] public bool clearOnEnable;
    }

    [Header("Trail Groups")]
    [Tooltip("Index in this array is the vfxId used by WeaponVfxWindow. Keep ids stable across weapons.")]
    [SerializeField] private VfxGroup[] vfxGroups = Array.Empty<VfxGroup>();

    private void Awake()
    {
        SetAll(false, clear: false);
    }

    public void SetVfx(int vfxId, bool enabled)
    {
        if ((uint)vfxId >= (uint)vfxGroups.Length) return;

        var group = vfxGroups[vfxId];
        var arr = group.vfxObjects;
        if (arr == null) return;

        foreach (var go in arr)
        {
            if (go == null) continue;

            go.SetActive(enabled);
        }
    }

    public void SetAll(bool enabled, bool clear)
    {
        var groups = vfxGroups;
        if (groups == null) return;

        for (int g = 0; g < groups.Length; g++)
        {
            var arr = groups[g].vfxObjects;
            if (arr == null) continue;

            foreach (var go in arr)
            {
                if (go == null) continue;

                go.SetActive(enabled);
            }
        }
    }
}
