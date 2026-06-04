using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableItemData", menuName = "Items/Consumable")]
public class ConsumableItemData : ItemData
{
    public List<BuffEffect> buffs;
}
