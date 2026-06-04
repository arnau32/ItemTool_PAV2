using UnityEngine;
using System.Collections.Generic;

// Manages all the base stats for any character
public class CharacterStats : MonoBehaviour
{
    [SerializeField] private CharacterBaseStatsSO _baseStatTemplate;
    [SerializeField] private List<CharacterStat> _baseStats = new();

    private Dictionary<Enums.StatType, CharacterStat> _statsDict;
    private List<CharacterStat> _statsList;

    #region Unity Callbacks

    private void Awake()
    {
        EnsureInit();
    }

    private void OnEnable()
    {
        EnsureInit();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        _statsDict = null;
        _statsList = null;
        EnsureInit();
    }

    #endregion

    #region Public API

    public float GetStatValue(Enums.StatType type)
    {
        EnsureInit();

        if (_statsDict.TryGetValue(type, out var stat)) return stat.Value;

        Debug.LogWarning($"Stat {type} not found in {name}");
        return 0;
    }

    public CharacterStat GetStat(Enums.StatType type)
    {
        EnsureInit();
        if (_statsDict.TryGetValue(type, out var stat)) return stat;

        Debug.LogWarning($"Stat {type} not found in {name}");
        return null;
    }

    public void AddModifier(Enums.StatType type, StatModifier modifier)
    {
        EnsureInit();
        if (_statsDict.TryGetValue(type, out var stat))
        {
            stat.AddModifier(modifier);
        }
        else
        {
            Debug.LogWarning($"Stat {type} not found in {name}");
        }
    }

    public void RemoveModifier(Enums.StatType type, StatModifier modifier)
    {
        EnsureInit();
        if (_statsDict.TryGetValue(type, out var stat))
        {
            stat.RemoveModifier(modifier);
        }
    }

    // C-4: iterates the cached list with for — zero enumerator allocation.
    public void RemoveModifiersFromSource(object source)
    {
        EnsureInit();
        for (int i = 0; i < _statsList.Count; i++)
        {
            _statsList[i].RemoveAllModifiersFromSource(source);
        }
    }

    #endregion

    private void EnsureInit()
    {
        if (_statsDict != null) return;

        _statsDict = new Dictionary<Enums.StatType, CharacterStat>();
        _statsList = new List<CharacterStat>();

        if (_baseStatTemplate != null)
        {
            InitFromTemplate();
        }
        else
        {
            InitFromSerializedList();
        }
    }

    // Deep-copies SO entries into runtime CharacterStat instances — SO is never mutated.
    private void InitFromTemplate()
    {
        var entries = _baseStatTemplate.Stats;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var stat = new CharacterStat(entry.baseValue, entry.type);
            _statsDict[entry.type] = stat;
            _statsList.Add(stat);
        }
    }

    private void InitFromSerializedList()
    {
        _baseStats ??= new List<CharacterStat>();

        for (int i = 0; i < _baseStats.Count; i++)
        {
            var entry = _baseStats[i];
            if (entry == null) continue;

            // Overwrite if duplicates (latest wins)
            _statsDict[entry.statTypeAffected] = entry;
        }

        foreach (var stat in _statsDict.Values)
        {
            _statsList.Add(stat);
        }
    }
}
