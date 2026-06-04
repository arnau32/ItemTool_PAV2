using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central ISaveable for all Storage interactables in the Base scene.
/// </summary>
public class StorageSaveService : MonoBehaviour, IGameServices, IInitializable, IShutdownable
{
    private readonly Dictionary<string, List<ItemStack>> _liveStacks = new(32);

    private readonly Dictionary<string, List<ItemStackSaveData>> _pendingLoad = new(32);

    private StorageSaveableAdapter _adapter;

    // ── IInitializable ────────────────────────────────────────────────────────

    public void Initialize()
    {
        _adapter = new StorageSaveableAdapter(this);

        if (GameServices.TryGet<SaveService>(out var save))
        {
            save.RegisterSaveable(_adapter);
        }
    }

    // ── IShutdownable ─────────────────────────────────────────────────────────

    public void Shutdown()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(_adapter);
    }

    // ── Registration API ─────────────────────────────────────────────────────

    public void RegisterStorage(string storageId, List<ItemStack> liveItems)
    {
        if (string.IsNullOrEmpty(storageId))
        {
            Debug.LogError("[STORAGE_DBG] RegisterStorage — storageId is EMPTY ❌");
            return;
        }

        if (_liveStacks.ContainsKey(storageId))
        {
            Debug.LogError($"[STORAGE_DBG] RegisterStorage — DUPLICATE storageId '{storageId}' ❌");
            return;
        }

        _liveStacks[storageId] = liveItems;

        // If save data arrived before this Storage registered (ApplyFromSave ran first),
        // restore it now directly into the live list.
        if (!_pendingLoad.TryGetValue(storageId, out var saved)) return;

        _pendingLoad.Remove(storageId);
        RestoreItemsInto(liveItems, saved);
    }

    /// <summary>
    /// Called by Storage.OnDestroy(). Snapshots current live items into _pendingLoad
    /// BEFORE removing from _liveStacks so CaptureToSave (triggered by sceneUnloaded
    /// on the same frame) can still serialize the data correctly.
    ///
    /// KEY INSIGHT: Unity order on scene unload is:
    ///   1. OnDestroy() on all scene objects  ← Storage.OnDestroy runs here
    ///   2. sceneUnloaded event               ← SaveService.Save() → CaptureToSave runs here
    /// Without the snapshot, _liveStacks would be empty when CaptureToSave runs.
    /// </summary>
    public void UnregisterStorage(string storageId)
    {
        if (!_liveStacks.TryGetValue(storageId, out var liveItems))
        {
            Debug.LogWarning($"[STORAGE_DBG] UnregisterStorage '{storageId}' — not found in _liveStacks, skipping.");
            return;
        }

        // Snapshot current live items into _pendingLoad so CaptureToSave can still
        // read them after _liveStacks is cleared.
        var snapshot = SnapshotItems(liveItems);
        _pendingLoad[storageId] = snapshot;
        _liveStacks.Remove(storageId);

    }

    internal void CaptureToSave(SaveData data)
    {
        var blocks = data.storages.blocks;
        blocks.Clear();

        // Capture live storages (scene is still loaded).
        foreach (var kvp in _liveStacks)
        {
            var block = BuildBlock(kvp.Key, kvp.Value);
            blocks.Add(block);
        }

        // Capture snapshotted storages (OnDestroy ran this frame before sceneUnloaded).
        foreach (var kvp in _pendingLoad)
        {
            // Skip if already captured from _liveStacks (shouldn't happen, but guard anyway).
            if (_liveStacks.ContainsKey(kvp.Key)) continue;

            var block = new StorageBlockSaveData { storageId = kvp.Key };
            for (int i = 0; i < kvp.Value.Count; i++)
                block.stacks.Add(kvp.Value[i]);

            blocks.Add(block);
        }
    }

    internal void ApplyFromSave(SaveData data)
    {
        _pendingLoad.Clear();

        var blocks = data.storages.blocks;

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (string.IsNullOrEmpty(block.storageId)) continue;

            if (_liveStacks.TryGetValue(block.storageId, out var live))
            {
                // Already registered (additive load edge case) — restore immediately.
                RestoreItemsInto(live, block.stacks);
            }
            else
            {
                // Not yet registered — park until Storage.Awake() calls RegisterStorage().
                _pendingLoad[block.storageId] = block.stacks;
            }
        }
    }

    private static StorageBlockSaveData BuildBlock(string storageId, List<ItemStack> items)
    {
        var block = new StorageBlockSaveData { storageId = storageId };

        for (int i = 0; i < items.Count; i++)
        {
            var s = items[i];
            if (s?.data == null) continue;

            block.stacks.Add(ItemStackSaveHelper.Capture(s));
        }

        return block;
    }

    private static List<ItemStackSaveData> SnapshotItems(List<ItemStack> items)
    {
        var snapshot = new List<ItemStackSaveData>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            var s = items[i];
            if (s?.data == null) continue;

            snapshot.Add(ItemStackSaveHelper.Capture(s));
        }

        return snapshot;
    }

    private static void RestoreItemsInto(List<ItemStack> target, List<ItemStackSaveData> source)
    {
        target.Clear();

        if (source == null || source.Count == 0)return;

        if (!GameServices.TryGet<SaveService>(out var save))
        {
            Debug.LogError("[STORAGE_DBG] RestoreItemsInto — SaveService NOT FOUND ");
            return;
        }

        int resolved = 0;
        int failed = 0;

        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            var itemData = save.ResolveItem(entry.uniqueID);

            if (itemData == null)
            {
                Debug.LogWarning($"[STORAGE_DBG] RestoreItemsInto — uniqueID '{entry.uniqueID}' NOT resolved ❌");
                failed++;
                continue;
            }

            target.Add(ItemStackSaveHelper.Restore(entry, itemData));
            resolved++;
        }

    }
}

internal sealed class StorageSaveableAdapter : ISaveable
{
    private readonly StorageSaveService _service;
    public StorageSaveableAdapter(StorageSaveService service) => _service = service;
    public void CaptureToSave(SaveData data) => _service.CaptureToSave(data);
    public void ApplyFromSave(SaveData data) => _service.ApplyFromSave(data);
}