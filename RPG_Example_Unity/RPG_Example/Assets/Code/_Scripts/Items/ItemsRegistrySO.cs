using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Items/Item Registry")]
public class ItemRegistrySO : ScriptableObject
{
    [Tooltip("All ItemData assets in the project. Every item that can be saved must be here.")]
    public List<ItemData> items = new();

    // Built at runtime — not serialized.
    private Dictionary<string, ItemData> _lookup;

    public void Build()
    {
        _lookup = new Dictionary<string, ItemData>(items.Count);

        foreach (var item in items)
        {
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.uniqueID))
            {
                Debug.LogWarning($"[ItemRegistry] '{item.name}' has no uniqueID — skipped.");
                continue;
            }

            if (_lookup.ContainsKey(item.uniqueID))
            {
                Debug.LogWarning($"[ItemRegistry] Duplicate uniqueID '{item.uniqueID}' on '{item.name}' — skipped.");
                continue;
            }

            _lookup[item.uniqueID] = item;
        }

    }

    public ItemData Resolve(string uniqueID)
    {
        if (_lookup == null) Build();
        if (string.IsNullOrEmpty(uniqueID)) return null;

        if (_lookup.TryGetValue(uniqueID, out var item)) return item;

        return null;
    }

#if UNITY_EDITOR
    // Auto-validate in editor: warn about missing IDs or duplicates on SO save.
    private void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var item in items)
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.uniqueID))
                Debug.LogWarning($"[ItemRegistry] '{item.name}' has empty uniqueID.", item);
            else if (!seen.Add(item.uniqueID))
                Debug.LogWarning($"[ItemRegistry] Duplicate uniqueID '{item.uniqueID}'.", item);
        }
    }
#endif
}