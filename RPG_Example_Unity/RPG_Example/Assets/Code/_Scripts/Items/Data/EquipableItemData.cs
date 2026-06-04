using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItemData", menuName = "Items/Equipable")]
public class EquipableItemData : ItemData
{
    public Enums.EquipSlot equipSlot;
    public List<StatModifier> modifiers;
    public GameObject prefab;

    [Header("Tier")]
    [Min(0)] public int tier;

    [Header("Generation")]
    public Enums.EquipableRollMode rollMode = Enums.EquipableRollMode.RandomRarityRandomStats;
}
