using System;
using System.Collections.Generic;

// ── Storage persistence data ──────────────────────────────────────────────────

/// <summary>
/// Root container for all base Storage data inside SaveData.
/// One StorageBlockSaveData per unique storageId in the scene.
/// </summary>
[Serializable]
public class StoragesSaveData
{
    // JsonUtility cannot serialize Dictionary<K,V> — flat list keyed by storageId.
    // StorageSaveService rebuilds a runtime Dictionary from this on ApplyFromSave.
    public List<StorageBlockSaveData> blocks = new(16);
}

/// <summary>
/// Persisted state for one Storage interactable in the Base scene.
/// storageId must match the Storage._storageId field on the scene object.
/// Reuses ItemStackSaveData — same structure as PlayerInventory so the
/// same resolve/capture pipeline applies with no extra types.
/// </summary>
[Serializable]
public class StorageBlockSaveData
{
    public string storageId;
    public List<ItemStackSaveData> stacks = new();
}