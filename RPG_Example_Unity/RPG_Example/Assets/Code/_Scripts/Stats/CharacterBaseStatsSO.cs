using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterBaseStats", menuName = "Base Stats")]
public class CharacterBaseStatsSO : ScriptableObject
{
    [SerializeField] private List<StatEntry> _stats = new();

    [Serializable]
    public struct StatEntry
    {
        public Enums.StatType type;
        public float baseValue;
    }

    public IReadOnlyList<StatEntry> Stats => _stats;
}
