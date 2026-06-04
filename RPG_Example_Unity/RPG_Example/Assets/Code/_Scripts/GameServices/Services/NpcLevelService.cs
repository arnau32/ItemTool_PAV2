using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcLevelService : MonoBehaviour, IGameServices, IInitializable, ISaveable
{
    #region Fields

    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(4);
    private SaveService _save;

    #endregion

    #region Properties

    public event Action<string> OnNpcLevelChanged;

    #endregion

    #region IInitializable

    public void Initialize()
    {
        if (GameServices.TryGet(out _save))
            _save.RegisterSaveable(this);
    }

    #endregion

    #region Unity Callbacks

    private void OnDestroy()
    {
        _save?.UnregisterSaveable(this);
    }

    #endregion

    #region Public API

    public int GetLevel(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return 0;
        _levels.TryGetValue(npcId, out int level);
        return level;
    }

    public void LevelUp(string npcId, int maxLevel)
    {
        if (string.IsNullOrEmpty(npcId)) return;

        _levels.TryGetValue(npcId, out int current);

        if (current >= maxLevel) return;

        _levels[npcId] = current + 1;
        OnNpcLevelChanged?.Invoke(npcId);
        _save?.Save();
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        data.npcLevels.entries.Clear();

        foreach (var kvp in _levels)
        {
            data.npcLevels.entries.Add(new NpcLevelEntry
            {
                npcId = kvp.Key,
                level = kvp.Value
            });
        }
    }

    public void ApplyFromSave(SaveData data)
    {
        _levels.Clear();

        for (int i = 0; i < data.npcLevels.entries.Count; i++)
        {
            var entry = data.npcLevels.entries[i];
            if (!string.IsNullOrEmpty(entry.npcId))
                _levels[entry.npcId] = entry.level;
        }
    }

    #endregion
}
