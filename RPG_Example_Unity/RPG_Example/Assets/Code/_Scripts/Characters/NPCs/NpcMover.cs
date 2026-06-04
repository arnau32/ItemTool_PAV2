using System.Collections.Generic;
using UnityEngine;

/// Registers itself in a static registry keyed by _npcMoverId.
/// MoveNpcAction looks it up by that ID and calls TeleportTo().
/// Implements ISaveable so the last teleported position persists across sessions.
public class NpcMover : MonoBehaviour, ISaveable
{
    #region Fields

    [SerializeField] private string _npcMoverId;

    private static readonly Dictionary<string, NpcMover> _registry = new Dictionary<string, NpcMover>();

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (string.IsNullOrEmpty(_npcMoverId))
        {
            Debug.LogWarning($"[NpcMover] {gameObject.name} has no npcMoverId assigned — save/load skipped.", this);
            return;
        }

        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);
    }

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(_npcMoverId))
            _registry[_npcMoverId] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(_npcMoverId)
            && _registry.TryGetValue(_npcMoverId, out var current)
            && current == this)
        {
            _registry.Remove(_npcMoverId);
        }
    }

    private void OnDestroy()
    {
        if (string.IsNullOrEmpty(_npcMoverId)) return;

        if (GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(this);
    }

    #endregion

    #region Public API

    public static bool TryGet(string id, out NpcMover mover) =>
        _registry.TryGetValue(id, out mover);

    public void TeleportTo(Vector3 position, float yRotation)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        // Write directly to CurrentSave so the position survives scene unload.
        // OnDestroy() unregisters this saveable before OnSceneUnloaded fires,
        // so CaptureToSave() would never be called — same pattern as NpcActivationState.SetUnlocked().
        if (string.IsNullOrEmpty(_npcMoverId)) return;
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        var entry = GetOrCreateEntry(save.CurrentSave);
        entry.posX      = position.x;
        entry.posY      = position.y;
        entry.posZ      = position.z;
        entry.yRotation = yRotation;
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        var entry = GetOrCreateEntry(data);
        entry.posX      = transform.position.x;
        entry.posY      = transform.position.y;
        entry.posZ      = transform.position.z;
        entry.yRotation = transform.eulerAngles.y;
    }

    public void ApplyFromSave(SaveData data)
    {
        var entry = FindEntry(data);
        if (entry == null) return;

        TeleportTo(new Vector3(entry.posX, entry.posY, entry.posZ), entry.yRotation);
    }

    #endregion

    #region Helpers

    private NpcPositionEntry GetOrCreateEntry(SaveData data)
    {
        for (int i = 0; i < data.npcPositions.entries.Count; i++)
        {
            if (data.npcPositions.entries[i].npcMoverId == _npcMoverId)
                return data.npcPositions.entries[i];
        }

        var newEntry = new NpcPositionEntry { npcMoverId = _npcMoverId };
        data.npcPositions.entries.Add(newEntry);
        return newEntry;
    }

    private NpcPositionEntry FindEntry(SaveData data)
    {
        for (int i = 0; i < data.npcPositions.entries.Count; i++)
        {
            if (data.npcPositions.entries[i].npcMoverId == _npcMoverId)
                return data.npcPositions.entries[i];
        }

        return null;
    }

    #endregion
}
