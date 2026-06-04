using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root save file serialized to JSON via JsonUtility.
/// Pure data contract — no MonoBehaviour, no ScriptableObject references.
/// All SO references are stored as uniqueID strings and resolved at load time
/// by SaveService via its ItemData registry.
///
/// File location: Application.persistentDataPath/save.json
/// </summary>
[Serializable]
public class SaveData
{
    public MetaData          meta      = new();
    public PlayerSaveData    player    = new();
    public InventorySaveData inventory = new();
    public QuestsSaveData    quests    = new();
    public UpgradesSaveData  upgrades  = new();

    // All base Storage interactables (chests, stashes, etc.). Managed by StorageSaveService.
    public StoragesSaveData  storages  = new();

    // NPC activation states (locked/unlocked). Managed by NpcActivationState.
    public NpcsSaveData         npcs         = new();
    public NpcLevelsSaveData    npcLevels    = new();

    // World positions of NPCs that can be moved during dialogue. Managed by NpcMover.
    public NpcPositionsSaveData npcPositions = new();

    // Door open states. Managed by DoorInteractable.
    public DoorsSaveData              doors            = new();
    public ExtractionPointsSaveData   extractionPoints = new();
    public RepairablesSaveData        repairables      = new();
    public MapSaveData                map              = new();
    public MorranContractSaveData     morranContract   = new();
}

// ── Meta ─────────────────────────────────────────────────────────────────────

[Serializable]
public class MetaData
{
    // True once the OnBoarding sequence has been completed.
    // MainMenuEvents reads this to skip OnBoarding on subsequent plays.
    public bool cinematicCompleted;
    public bool onboardingCompleted;

    public bool tutorialConsumableHintShown;
    public bool abilityReadyHintShown;
    public bool knockdownHintShown;

    // True after the Aldwyn revival dialogue has been shown once (first death).
    public bool hasSeenRevivalDialogue;

    // True after Aldwyn heals the player during a village visit.
    // Reset automatically when the player returns from an expedition.
    public bool hasHealedThisVisit;

    // Last scene the player was in — used for "continue" logic (future).
    public string lastScene = SceneNames.Base;

    // 0 = Normal (KeepOnEnter), 1 = Easy (KeepAll).
    // Written once on New Game and persists for the entire save file.
    public int difficulty = 0;
}

// ── Player ───────────────────────────────────────────────────────────────────

[Serializable]
public class PlayerSaveData
{
    public float currentHealth  = -1f; // -1 means "use MaxHealth on load"
    public float currentStamina = -1f;

    // Last world position in a scene that opts into position saving (e.g. Village).
    // Restored by PlayerSpawner when UseSavedPosition is true.
    public float lastPosX, lastPosY, lastPosZ;
    public float lastYRotation;
    public bool  hasLastPosition;
}

// ── Inventory ────────────────────────────────────────────────────────────────

[Serializable]
public class InventorySaveData
{
    public int                          auroraDust             = 0;
    public int                          inventoryCapacityLevel = 0;
    public List<ItemStackSaveData>      inventoryStacks        = new();
    public List<EquipmentSlotSaveData>  equippedItems          = new();
    public List<ConsumableSlotSaveData> consumableSlots        = new();

    // Snapshot taken at Level entry. Null/empty list = no snapshot active.
    // Restored on death if DeathPenalty == KeepOnEnter.
    public List<ItemStackSaveData>      levelEntryInventorySnapshot  = new();
    public List<EquipmentSlotSaveData>  levelEntryEquipmentSnapshot  = new();
    public List<ConsumableSlotSaveData> levelEntryConsumableSnapshot = new();
    public bool hasLevelSnapshot;
}

[Serializable]
public class StatModifierSaveData
{
    public int   statType;
    public int   modifierType;
    public float value;
    public int   order;
}

[Serializable]
public class ItemStackSaveData
{
    public string uniqueID;
    public int    quantity;
    public int    gridX;
    public int    gridY;
    public int    rotationIndex;
    public bool   rotated;
    public bool   isInstanced;
    public string runtimeInstanceId;
    public int    rolledRarity;
    public List<StatModifierSaveData> rolledModifiers = new();
}

[Serializable]
public class EquipmentSlotSaveData
{
    public string slotName;
    public string uniqueID;
    public int    quantity;
    public bool   isInstanced;
    public string runtimeInstanceId;
    public int    rolledRarity;
    public List<StatModifierSaveData> rolledModifiers = new();
}

[Serializable]
public class ConsumableSlotSaveData
{
    public int    slotNumber;
    public string uniqueID;
    public int    quantity;
}

// ── Quests ───────────────────────────────────────────────────────────────────

// Wraps all quest saves. QuestService migrates from PlayerPrefs to this on first load.
[Serializable]
public class QuestsSaveData
{
    public List<QuestSaveEntry> entries = new();

    // Interaction IDs reported across all sessions. Used by InteractMultipleStepSO
    // to retroactively count NPCs interacted with before the quest was accepted.
    public List<string> reportedInteractions = new();
}

[Serializable]
public class QuestSaveEntry
{
    public string id;
    public string json; // JsonUtility.ToJson(QuestData) — keeps QuestService's existing format
}

// ── Upgrades ──────────────────────────────────────────────────────────────────

// Stores the purchased level for each UpgradeNodeSO slot in PlayerUpgradeService._nodes.
// Index in the list matches index in the inspector array.
[Serializable]
public class UpgradesSaveData
{
    public List<int> levels = new();
}

// ── NPCs ──────────────────────────────────────────────────────────────────────

[Serializable]
public class NpcsSaveData
{
    public List<NpcSaveEntry> entries = new();
}

[Serializable]
public class NpcSaveEntry
{
    public string npcId;
    public bool   isUnlocked;
}

// ── NPC Positions ─────────────────────────────────────────────────────────────

[Serializable]
public class NpcPositionsSaveData
{
    public List<NpcPositionEntry> entries = new();
}

[Serializable]
public class NpcPositionEntry
{
    public string npcMoverId;
    public float  posX, posY, posZ;
    public float  yRotation;
}

// ── NPC Levels ────────────────────────────────────────────────────────────────

[Serializable]
public class NpcLevelsSaveData
{
    public List<NpcLevelEntry> entries = new();
}

[Serializable]
public class NpcLevelEntry
{
    public string npcId;
    public int    level;
}

// ── Doors ─────────────────────────────────────────────────────────────────────

[Serializable]
public class DoorsSaveData
{
    public List<DoorSaveEntry> entries = new();
}

[Serializable]
public class DoorSaveEntry
{
    public string doorId;
    public bool   isOpen;
}

// ── Extraction Points ─────────────────────────────────────────────────────────

[Serializable]
public class ExtractionPointsSaveData
{
    public List<ExtractionPointSaveEntry> entries = new();
}

[Serializable]
public class ExtractionPointSaveEntry
{
    public string pointId;
    public bool   isActivated;
}

// ── Repairables ───────────────────────────────────────────────────────────────

[Serializable]
public class RepairablesSaveData
{
    public List<RepairableSaveEntry> entries = new();

    public bool IsRepaired(string id)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].id == id) return entries[i].isRepaired;
        }
        return false;
    }

    public void SetRepaired(string id, bool value)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].id != id) continue;
            entries[i].isRepaired = value;
            return;
        }
        entries.Add(new RepairableSaveEntry { id = id, isRepaired = value });
    }
}

[Serializable]
public class RepairableSaveEntry
{
    public string id;
    public bool   isRepaired;
}

// ── Map ───────────────────────────────────────────────────────────────────────

// One entry per Map SO (keyed by Map.name). Keeps fog and POIs isolated per scene.
[Serializable]
public class MapSaveData
{
    public List<MapEntrySaveData> entries = new();

    public MapEntrySaveData GetOrCreate(string mapId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].mapId == mapId) return entries[i];
        }
        var entry = new MapEntrySaveData { mapId = mapId };
        entries.Add(entry);
        return entry;
    }

    public MapEntrySaveData Find(string mapId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].mapId == mapId) return entries[i];
        }
        return null;
    }
}

[Serializable]
public class MapEntrySaveData
{
    public string       mapId;
    public List<string> discoveredPOIIds     = new();
    public string       explorationMaskBase64 = "";
}

// ── Save helpers ──────────────────────────────────────────────────────────────

public static class ItemStackSaveHelper
{
    public static ItemStackSaveData Capture(ItemStack stack)
    {
        var entry = new ItemStackSaveData
        {
            uniqueID          = stack.data.uniqueID,
            quantity          = stack.quantity,
            gridX             = stack.gridX,
            gridY             = stack.gridY,
            rotationIndex     = stack.rotationIndex,
            rotated           = stack.rotated,
            isInstanced       = stack.isInstanced,
        };

        if (stack.isInstanced)
        {
            entry.runtimeInstanceId = stack.runtimeInstanceId;
            entry.rolledRarity      = (int)stack.rolledRarity;
            entry.rolledModifiers   = CaptureModifiers(stack.rolledModifiers);
        }

        return entry;
    }

    public static ItemStack Restore(ItemStackSaveData entry, ItemData data)
    {
        var stack = new ItemStack
        {
            data          = data,
            quantity      = entry.quantity,
            gridX         = entry.gridX,
            gridY         = entry.gridY,
            rotationIndex = entry.rotationIndex,
            rotated       = entry.rotated,
            isInstanced   = entry.isInstanced,
        };

        if (entry.isInstanced)
        {
            stack.runtimeInstanceId = entry.runtimeInstanceId;
            stack.rolledRarity      = (Enums.ItemRarity)entry.rolledRarity;
            stack.rolledModifiers   = RestoreModifiers(entry.rolledModifiers);
        }

        return stack;
    }

    public static EquipmentSlotSaveData CaptureEquipSlot(Enums.EquipSlot slot, EquipableItemData item, ItemStack stack)
    {
        var entry = new EquipmentSlotSaveData
        {
            slotName = slot.ToString(),
            uniqueID = item.uniqueID,
            quantity = 1,
        };

        if (stack != null && stack.isInstanced)
        {
            entry.isInstanced       = true;
            entry.runtimeInstanceId = stack.runtimeInstanceId;
            entry.rolledRarity      = (int)stack.rolledRarity;
            entry.rolledModifiers   = CaptureModifiers(stack.rolledModifiers);
        }

        return entry;
    }

    public static ItemStack RestoreEquipableStack(EquipmentSlotSaveData entry, EquipableItemData itemData)
    {
        var stack = new ItemStack
        {
            data        = itemData,
            quantity    = 1,
            isInstanced = entry.isInstanced,
        };

        if (entry.isInstanced)
        {
            stack.runtimeInstanceId = entry.runtimeInstanceId;
            stack.rolledRarity      = (Enums.ItemRarity)entry.rolledRarity;
            stack.rolledModifiers   = RestoreModifiers(entry.rolledModifiers);
        }

        return stack;
    }

    private static List<StatModifierSaveData> CaptureModifiers(List<StatModifier> modifiers)
    {
        var result = new List<StatModifierSaveData>(modifiers?.Count ?? 0);
        if (modifiers == null) return result;

        for (int i = 0; i < modifiers.Count; i++)
        {
            var m = modifiers[i];
            if (m == null) continue;

            result.Add(new StatModifierSaveData
            {
                statType     = (int)m.statTypeAffected,
                modifierType = (int)m.type,
                value        = m.value,
                order        = m.order
            });
        }

        return result;
    }

    private static List<StatModifier> RestoreModifiers(List<StatModifierSaveData> saved)
    {
        var result = new List<StatModifier>(saved?.Count ?? 0);
        if (saved == null) return result;

        for (int i = 0; i < saved.Count; i++)
        {
            var s = saved[i];
            result.Add(new StatModifier
            {
                statTypeAffected = (Enums.StatType)s.statType,
                type             = (StatModifierType)s.modifierType,
                value            = s.value,
                order            = s.order
            });
        }

        return result;
    }
}

// ── Morran Contracts ──────────────────────────────────────────────────────────

[Serializable]
public class MorranContractSaveData
{
    public string activeContractId = "";
}

// Scene name constants

public static class SceneNames
{
    public const string Menu       = "Menu";
    public const string Cinematic       = "Cinematic";
    public const string OnBoarding = "OnBoarding";
    public const string Base       = "Village";
    public const string Level      = "Map"; // prefix for level scenes ("Level_01" etc.)

    public static bool IsLevel(string sceneName) =>
        !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith(Level);
}