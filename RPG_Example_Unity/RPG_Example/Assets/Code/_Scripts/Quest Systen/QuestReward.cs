using UnityEngine;

[System.Serializable]
public class QuestReward
{
    [System.Serializable]
    public struct ItemReward
    {
        public ItemData item;
        [Min(1)] public int quantity;
    }

    [System.Serializable]
    public struct RandomItemReward
    {
        [Tooltip("One item is chosen at random from this pool when the quest is completed.")]
        public ItemData[] pool;

        [Min(1)] public int quantity;

        [Header("Rarity Override")]
        [Tooltip("Forces a specific rarity instead of the item's default roll mode. Only applies to equippable items.")]
        public bool overrideRarity;

        [Tooltip("Quality tier applied to the reward when overrideRarity is true.")]
        public Enums.ItemRarity forcedRarity;
    }

    [Header("Item Rewards")]
    public ItemReward[] items;

    [Header("Random Item Rewards")]
    public RandomItemReward[] randomItems;

    [Header("Aurora Dust")]
    [Min(0)] public int auroraDust;
}