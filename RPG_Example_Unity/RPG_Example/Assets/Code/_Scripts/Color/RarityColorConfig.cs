using static Enums;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[System.Serializable]
public struct RarityColorEntry
{
    public ItemRarity rarity;
    public Color color;
    public LocalizedString localizedName;
}

[CreateAssetMenu(fileName = "RarityColorConfig", menuName = "Game/Config/Rarity Colors")]
public class RarityColorConfig : ScriptableObject
{
    public RarityColorEntry[] entries;

    private Dictionary<ItemRarity, Color> _colorDict;
    private Dictionary<ItemRarity, LocalizedString> _nameDict;

    public void Init()
    {
        _colorDict = new Dictionary<ItemRarity, Color>(entries.Length);
        _nameDict  = new Dictionary<ItemRarity, LocalizedString>(entries.Length);
        foreach (var e in entries)
        {
            _colorDict[e.rarity] = e.color;
            _nameDict[e.rarity]  = e.localizedName;
        }
    }

    public Color GetColor(ItemRarity rarity)
    {
        if (_colorDict == null) Init();
        return _colorDict.TryGetValue(rarity, out var color) ? color : Color.white;
    }

    public LocalizedString GetLocalizedName(ItemRarity rarity)
    {
        if (_nameDict == null) Init();
        return _nameDict.TryGetValue(rarity, out var ls) ? ls : null;
    }
}