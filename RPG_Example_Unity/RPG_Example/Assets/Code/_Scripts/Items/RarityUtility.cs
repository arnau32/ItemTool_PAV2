using UnityEngine;

public static class RarityUtility
{
    public static (float min, float max) GetMultiplierRange(Enums.ItemRarity rarity)
    {
        return rarity switch
        {
            Enums.ItemRarity.Common    => (0.85f, 1.05f),
            Enums.ItemRarity.Rare      => (1.1f, 1.2f),
            Enums.ItemRarity.Epic      => (1.25f, 1.35f),
            Enums.ItemRarity.Legendary => (1.45f, 1.60f),
            _ => (1.0f, 1.0f)
        };
    }

    public static Enums.ItemRarity RollRandomRarity()
    {
        float roll = Random.value;

        if (roll < 0.45f) return Enums.ItemRarity.Common;
        if (roll < 0.75f) return Enums.ItemRarity.Rare;
        if (roll < 0.95f) return Enums.ItemRarity.Epic;
        return Enums.ItemRarity.Legendary;
    }
    
}