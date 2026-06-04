using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central save/load coordinator. Registered in GameBootstrap as a persistent service.
///
/// Responsibilities:
///   - Owns the single SaveData instance in memory (CurrentSave).
///   - Reads / writes the JSON file from Application.persistentDataPath.
///   - Maintains the ItemData registry (loaded from Resources/Items/) so that
///     uniqueID strings in the JSON can be resolved back to SO references at runtime.
///   - Calls ISaveable.CaptureToSave / ApplyFromSave on all registered systems.
///   - Manages the Level-entry snapshot for DeathPenalty.KeepOnEnter.
///   - Triggers Save on scene unload and on application quit.
///
/// Systems register themselves via RegisterSaveable(). Order matters for ApplyFromSave:
/// register stats/health before inventory, inventory before equipment.
///
/// DEATH PENALTY LOCK:
///   After ApplyDeathPenalty() mutates _current (wipes inventory, resets health),
///   any subsequent CaptureAll() call would re-read live scene state and overwrite
///   those mutations. This happens via:
///     - PlayerSaveHandler.OnDisable() → CaptureToSave (fires before sceneUnloaded)
///     - OnSceneUnloaded → Save() → CaptureAll()
///     - Shutdown() → Save() → CaptureAll() (Alt+F4)
///   Fix: _deathPenaltyApplied flag blocks CaptureAll() after ApplyDeathPenalty().
///   Reset on OnSceneLoaded so normal saves work again in the next scene.
/// </summary>
public class SaveService : MonoBehaviour, IGameServices, IInitializable, IShutdownable
{
    // ── Config ────────────────────────────────────────────────────────────────

    [Header("Death Penalty")]
    [Tooltip("Global rule applied when the player dies inside a Level scene.")]
    [SerializeField] private Enums.DeathPenalty _deathPenalty = Enums.DeathPenalty.KeepOnEnter;

    [Header("Item Registry")]
    [Tooltip("Assign the ItemRegistry SO here. If left empty, SaveService will fall back to " +
             "Resources.LoadAll<ItemData>(\"Items\") — requires all items to be in Resources/Items/.")]
    [SerializeField] private ItemRegistrySO _itemRegistrySO;

    // ── State ─────────────────────────────────────────────────────────────────

    private SaveData _current;
    private readonly List<ISaveable> _saveables = new(8);

    // Cached item registry: uniqueID → ItemData SO
    private readonly Dictionary<string, ItemData> _itemRegistry = new(64);

    // True once ApplyAll() has completed for the current scene load.
    // Systems that load async (PlayerInventory.LoadItems) wait on this
    // before attempting to place visuals.
    private bool _saveApplied;

    // True after ApplyDeathPenalty() runs. Blocks CaptureAll() so that
    // OnDisable / OnSceneUnloaded / Shutdown cannot overwrite the penalty
    // mutations with stale live-scene data. Reset on OnSceneLoaded.
    private bool _deathPenaltyApplied;

    // True after WipeSave() until the first scene loads. Blocks CaptureAll()
    // so that OnSceneUnloaded (fired when the current scene transitions out)
    // cannot re-capture stale in-memory state from persistent services
    // (QuestService, NpcLevelService, etc.) back into the fresh SaveData.
    private bool _wipePending;

    private static string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, "save.json");

    // ── Public API ────────────────────────────────────────────────────────────

    public SaveData CurrentSave => _current;
    public Enums.DeathPenalty DeathPenalty => _deathPenalty;
    public int CurrentDifficulty => _current.meta.difficulty;

    /// <summary>
    /// True once ApplyAll() has finished for the current scene.
    /// Async systems (PlayerInventory.LoadItems) must wait on this
    /// before placing visuals so they don't read empty ItemStacks.
    /// Reset to false on every scene load, set to true after ApplyAll().
    /// </summary>
    public bool IsSaveApplied => _saveApplied;

    /// <summary>
    /// True after ApplyDeathPenalty() runs, until the next scene loads.
    /// Used by PlayerSaveHandler.OnDisable() to skip direct CaptureToSave()
    /// calls that would overwrite the cleared equipment/consumable data with
    /// the live scene state (items still on the player's GameObject).
    /// </summary>
    public bool IsDeathPenaltyApplied => _deathPenaltyApplied;

    /// <summary>
    /// Register a system to participate in save/load.
    /// Call this from the system's Awake or after GameBootstrap finishes.
    /// </summary>
    public void RegisterSaveable(ISaveable saveable)
    {
        if (!_saveables.Contains(saveable))
            _saveables.Add(saveable);
    }

    public void UnregisterSaveable(ISaveable saveable) => _saveables.Remove(saveable);

    /// <summary>
    /// Resolves a uniqueID string to its ItemData ScriptableObject.
    /// Uses ItemRegistrySO if assigned, otherwise falls back to the internal
    /// dictionary built from Resources.LoadAll.
    /// Returns null and logs a warning if not found.
    /// </summary>
    public ItemData ResolveItem(string uniqueID)
    {
        if (string.IsNullOrEmpty(uniqueID)) return null;

        if (_itemRegistrySO != null) return _itemRegistrySO.Resolve(uniqueID);

        if (_itemRegistry.TryGetValue(uniqueID, out var item)) return item;

        Debug.LogWarning($"[SaveService] ItemData with uniqueID '{uniqueID}' not found. " +
                         "Assign an ItemRegistry SO to SaveService, or place items in Resources/Items/.");
        return null;
    }

    /// <summary>
    /// Saves a Level-entry snapshot for DeathPenalty.KeepOnEnter.
    /// Called by ExpeditionManager (or whoever loads the Level scene) before entering.
    /// </summary>
    public void TakeLevelEntrySnapshot()
    {
        CaptureAll();

        var inv = _current.inventory;
        inv.levelEntryInventorySnapshot  = DeepCopyList(inv.inventoryStacks);
        inv.levelEntryEquipmentSnapshot  = DeepCopyList(inv.equippedItems);
        inv.levelEntryConsumableSnapshot = DeepCopyList(inv.consumableSlots);
        inv.hasLevelSnapshot             = true;

        Debug.Log("[SaveService] Level-entry snapshot taken.");
    }

    /// <summary>
    /// Clears the snapshot once the player extracts successfully (returns to Base alive).
    /// </summary>
    public void ClearLevelSnapshot()
    {
        var inv = _current.inventory;
        inv.levelEntryInventorySnapshot.Clear();
        inv.levelEntryEquipmentSnapshot.Clear();
        inv.levelEntryConsumableSnapshot.Clear();
        inv.hasLevelSnapshot = false;
    }

    /// <summary>
    /// Applies the death penalty to the in-memory CurrentSave.
    /// Called by PlayerLifeController.HandleDead() before loading Base.
    /// Does NOT write to disk — call SaveImmediate() right after to persist.
    ///
    /// Sets _deathPenaltyApplied = true to block any subsequent CaptureAll()
    /// from overwriting the penalty mutations with stale live-scene data.
    /// The flag is reset on OnSceneLoaded so normal saves resume in Base.
    /// </summary>
    public void ApplyDeathPenalty()
    {
        // Capture current state BEFORE mutating — this is the last valid live capture.
        CaptureAll();

        switch (_deathPenalty)
        {
            case Enums.DeathPenalty.KeepAll:
                // Nothing changes.
                break;

            case Enums.DeathPenalty.KeepOnEnter:
                if (_current.inventory.hasLevelSnapshot)
                    RestoreFromLevelSnapshot();
                else
                    Debug.LogWarning("[SaveService] KeepOnEnter penalty but no snapshot found — keeping current inventory.");
                break;

            case Enums.DeathPenalty.LoseAll:
                _current.inventory.inventoryStacks.Clear();
                _current.inventory.equippedItems.Clear();
                _current.inventory.consumableSlots.Clear();
                break;
        }

        // Clear snapshot regardless — it is only valid for one Level trip.
        ClearLevelSnapshot();

        // Restore health to full for the respawn.
        _current.player.currentHealth  = -1f; // -1 = use MaxHealth on load
        _current.player.currentStamina = -1f;

        // Lock CaptureAll() so OnDisable / OnSceneUnloaded / Shutdown cannot
        // overwrite the mutations above with stale live-scene data.
        _deathPenaltyApplied = true;
    }

    /// <summary>
    /// Captures all ISaveable state then writes to disk.
    /// Use this for normal saves (scene transitions, quit).
    /// DO NOT call after ApplyDeathPenalty() — use SaveImmediate() instead
    /// to avoid CaptureAll() overwriting the penalty that was just applied.
    /// </summary>
    public void Save()
    {
        CaptureAll();
        WriteToDisk();
        Debug.Log($"[SaveService] Saved to {SaveFilePath}");
    }

    /// <summary>
    /// Writes CurrentSave to disk WITHOUT calling CaptureAll() first.
    /// Use after ApplyDeathPenalty() so the penalty mutations are preserved.
    /// </summary>
    public void SaveImmediate()
    {
        WriteToDisk();
        Debug.Log($"[SaveService] SaveImmediate to {SaveFilePath}");
    }

    /// <summary>Read from disk into CurrentSave and apply to all saveables.</summary>
    public void Load()
    {
        ReadFromDisk();
        ApplyAll();
    }

    // ── IInitializable ────────────────────────────────────────────────────────

    public void Initialize()
    {
        if (_itemRegistrySO != null)
            _itemRegistrySO.Build();
        else
            BuildItemRegistry();

        ReadFromDisk();

        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    // ── IShutdownable ─────────────────────────────────────────────────────────

    public void Shutdown()
    {
        SceneManager.sceneLoaded   -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Save();
    }

    // ── Capture / Apply all saveables ─────────────────────────────────────────

    /// <summary>
    /// Captures all registered saveables into CurrentSave.
    /// Blocked when _deathPenaltyApplied is true — after ApplyDeathPenalty()
    /// the in-memory data is the source of truth and must not be overwritten
    /// by live scene state from OnDisable / OnSceneUnloaded / Shutdown.
    /// </summary>
    public void CaptureAll()
    {
        if (_deathPenaltyApplied)
        {
            Debug.Log("[SaveService] CaptureAll skipped — death penalty lock is active.");
            return;
        }

        if (_wipePending)
        {
            Debug.Log("[SaveService] CaptureAll skipped — wipe pending.");
            return;
        }

        for (int i = 0; i < _saveables.Count; i++)
        {
            try { _saveables[i].CaptureToSave(_current); }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] CaptureToSave failed on {_saveables[i]}: {e}");
            }
        }
    }

    /// <summary>
    /// Applies CurrentSave to all registered saveables.
    /// Called after reading from disk and when systems are ready.
    /// </summary>
    public void ApplyAll()
    {
        for (int i = 0; i < _saveables.Count; i++)
        {
            try { _saveables[i].ApplyFromSave(_current); }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] ApplyFromSave failed on {_saveables[i]}: {e}");
            }
        }
    }


    public void WipeSave()
    {
        _current = new SaveData();
        _deathPenaltyApplied = false;
        _wipePending = true;
        WriteToDisk();
    }

    /// <summary>
    /// Stores the chosen difficulty in the save and updates the active death penalty.
    /// Call immediately after WipeSave() on New Game, before loading the first scene.
    /// 0 = Normal (KeepOnEnter), 1 = Easy (KeepAll).
    /// </summary>
    public void SetDifficulty(int difficulty)
    {
        _current.meta.difficulty = difficulty;
        SyncDifficultyFromSave();
    }

    // ── Scene hooks ───────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _deathPenaltyApplied = false;
        _wipePending = false;
        SyncDifficultyFromSave();

        // Player returned from an expedition (death or extraction) — allow healing again.
        if (scene.name == SceneNames.Base && SceneNames.IsLevel(_current.meta.lastScene))
            _current.meta.hasHealedThisVisit = false;

        // Reset the flag — scene systems will re-register in their Awake()
        // before this runs (sceneLoaded fires after all Awake() calls).
        // ApplyAll() populates ItemStacks, QuestService state, etc.
        // PlayerInventory.LoadItems() waits on IsSaveApplied before placing visuals.
        _saveApplied = false;
        ApplyAll();
        _saveApplied = true;

        if (scene.name == SceneNames.Base || SceneNames.IsLevel(scene.name))
            _current.meta.lastScene = scene.name;

        if (SceneNames.IsLevel(scene.name) && _deathPenalty != Enums.DeathPenalty.KeepAll)
            TakeLevelEntrySnapshot();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        _saveApplied = false;
        Save();
    }

    // ── Disk I/O ──────────────────────────────────────────────────────────────

    private void ReadFromDisk()
    {
        if (!File.Exists(SaveFilePath))
        {
            _current = new SaveData();
            Debug.Log("[SaveService] No save file found — starting fresh.");
            SyncDifficultyFromSave();
            return;
        }

        try
        {
            var json = File.ReadAllText(SaveFilePath);
            _current = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            Debug.Log("[SaveService] Save file loaded.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to read save file: {e}. Starting fresh.");
            _current = new SaveData();
        }

        SyncDifficultyFromSave();
    }

    private void WriteToDisk()
    {
        try
        {
            var json = JsonUtility.ToJson(_current, prettyPrint: true);
            File.WriteAllText(SaveFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to write save file: {e}");
        }
    }

    // ── Item registry ─────────────────────────────────────────────────────────

    private void BuildItemRegistry()
    {
        _itemRegistry.Clear();
        var allItems = Resources.LoadAll<ItemData>("Items");

        for (int i = 0; i < allItems.Length; i++)
        {
            var item = allItems[i];
            if (string.IsNullOrEmpty(item.uniqueID))
            {
                Debug.LogWarning($"[SaveService] ItemData '{item.name}' has no uniqueID — skipped.");
                continue;
            }

            if (_itemRegistry.ContainsKey(item.uniqueID))
            {
                Debug.LogWarning($"[SaveService] Duplicate uniqueID '{item.uniqueID}' on '{item.name}' — skipped.");
                continue;
            }

            _itemRegistry[item.uniqueID] = item;
        }

        Debug.Log($"[SaveService] Item registry built: {_itemRegistry.Count} items.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RestoreFromLevelSnapshot()
    {
        var inv = _current.inventory;
        inv.inventoryStacks  = DeepCopyList(inv.levelEntryInventorySnapshot);
        inv.equippedItems    = DeepCopyList(inv.levelEntryEquipmentSnapshot);
        inv.consumableSlots  = DeepCopyList(inv.levelEntryConsumableSnapshot);
    }

    // Reads difficulty from the active save and updates _deathPenalty to match.
    // Called after ReadFromDisk, WipeSave+SetDifficulty, and each scene load so that
    // Continue Game always restores the difficulty chosen at the start of that save file.
    private void SyncDifficultyFromSave()
    {
        _deathPenalty = _current.meta.difficulty == 1
            ? Enums.DeathPenalty.KeepAll
            : Enums.DeathPenalty.LoseAll;
    }

    // JsonUtility deep-copy: serialize → deserialize into a new list.
    // Only works for [Serializable] types, which all our SaveData types are.
    private static List<T> DeepCopyList<T>(List<T> source)
    {
        if (source == null || source.Count == 0) return new List<T>();

        // Wrap in a container because JsonUtility can't serialize bare lists.
        var wrapper = new ListWrapper<T> { items = source };
        var json    = JsonUtility.ToJson(wrapper);
        var copy    = JsonUtility.FromJson<ListWrapper<T>>(json);
        return copy.items ?? new List<T>();
    }

    [Serializable]
    private class ListWrapper<T> { public List<T> items; }
}