using UnityEngine;
using UnityEngine.Events;

// Add to the same GameObject as NPC or NPC_QuestGiver. GO must start ACTIVE in scene.
public class NpcActivationState : MonoBehaviour, ISaveable
{
    #region Fields

    [Tooltip("Unique identifier for this NPC in the save file. Must be unique across all scenes.")]
    [SerializeField] private string _npcId;

    [Tooltip("If true, the NPC starts inactive on a fresh save (no existing entry).")]
    [SerializeField] private bool _startsLocked;

    [Space]
    [SerializeField] private UnityEvent _onUnlocked;
    [SerializeField] private UnityEvent _onLocked;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (string.IsNullOrEmpty(_npcId))
        {
            Debug.LogWarning($"[NpcActivationState] {gameObject.name} has no npcId assigned — save/load skipped.", this);
            return;
        }

        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);
    }

    private void OnDestroy()
    {
        if (string.IsNullOrEmpty(_npcId)) return;

        if (GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(this);
    }

    #endregion

    #region Public API

    public void Unlock()
    {
        gameObject.SetActive(true);
        SetUnlocked(_npcId, true);
        _onUnlocked?.Invoke();
    }

    public void Lock()
    {
        gameObject.SetActive(false);
        SetUnlocked(_npcId, false);
        _onLocked?.Invoke();
    }

    // Use this to unlock an NPC that lives in a different (unloaded) scene.
    public static void SetUnlocked(string npcId, bool isUnlocked)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        var entry = GetOrCreateEntryById(save.CurrentSave, npcId);
        entry.isUnlocked = isUnlocked;
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        var entry = GetOrCreateEntry(data);
        entry.isUnlocked = gameObject.activeSelf;
    }

    public void ApplyFromSave(SaveData data)
    {
        var entry = FindEntry(data);
        bool isUnlocked = entry != null ? entry.isUnlocked : !_startsLocked;
        gameObject.SetActive(isUnlocked);

        if (isUnlocked)
            _onUnlocked?.Invoke();
        else
            _onLocked?.Invoke();
    }

    #endregion

    #region Helpers

    private NpcSaveEntry GetOrCreateEntry(SaveData data)
    {
        return GetOrCreateEntryById(data, _npcId, !_startsLocked);
    }

    private NpcSaveEntry FindEntry(SaveData data)
    {
        for (int i = 0; i < data.npcs.entries.Count; i++)
        {
            if (data.npcs.entries[i].npcId == _npcId)
                return data.npcs.entries[i];
        }

        return null;
    }

    private static NpcSaveEntry GetOrCreateEntryById(SaveData data, string npcId, bool defaultUnlocked = true)
    {
        for (int i = 0; i < data.npcs.entries.Count; i++)
        {
            if (data.npcs.entries[i].npcId == npcId)
                return data.npcs.entries[i];
        }

        var newEntry = new NpcSaveEntry { npcId = npcId, isUnlocked = defaultUnlocked };
        data.npcs.entries.Add(newEntry);
        return newEntry;
    }

    #endregion
}
