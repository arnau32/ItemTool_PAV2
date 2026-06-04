using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemStack
{
    public ItemData data;
    public ItemVisual RootVisual;
    public int quantity;

    [Header("Runtime Instance Data")]
    public bool isInstanced = false;
    public string runtimeInstanceId;
    public Enums.ItemRarity rolledRarity = Enums.ItemRarity.Common;
    public List<StatModifier> rolledModifiers = new();

    public int gridX;
    public int gridY;
    public int rotationIndex = 0;
    public bool rotated = false;

    public bool IsEmpty => quantity <= 0;
    public bool HasRolledData => isInstanced;

    public bool IsStackable
    {
        get
        {
            if (isInstanced) return false;
            if (data == null) return false;
            return data.maxStack > 1;
        }
    }

    public Enums.ItemRarity GetEffectiveRarity()
    {
        if (isInstanced)
            return rolledRarity;

        if (data != null)
            return data.itemRarity;

        return Enums.ItemRarity.Common;
    }

    public List<StatModifier> GetEffectiveModifiers()
    {
        if (isInstanced)
            return rolledModifiers;

        if (data is EquipableItemData equipable)
            return equipable.modifiers;

        return null;
    }

    public void Add(int amount)
    {
        quantity = Mathf.Clamp(quantity + amount, 0, data.maxStack);
    }

    public void Remove(int amount)
    {
        quantity = Mathf.Max(quantity - amount, 0);
    }

    public bool Use(GameObject user)
    {
        switch (data.itemType)
        {
            case Enums.ItemType.Consumable:
                return TryUseConsumable(user, data);

            default:
                Debug.LogWarning($"Item of type {data.itemType} cannot be used.");
                return false;
        }
    }

    private bool TryUseConsumable(GameObject user, ItemData consumbale)
    {
        var consumable = data as ConsumableItemData;
        if (consumable.buffs is { Count: > 0 })
        {
            var buffHandler = user.GetComponent<BuffHandler>();
            if (buffHandler == null)
            {
                Debug.LogWarning($"[ItemStack] {user.name} has no BuffHandler — buffs skipped.");
            }
            else
            {
                foreach (var buff in consumable.buffs)
                {
                    buffHandler.ApplyBuff(buff, (consumable, buff.statType));
                }
            }
        }

        Remove(1);
        return true;
    }

    public ItemStack Clone()
    {
        var clone = new ItemStack
        {
            data = this.data,
            RootVisual = this.RootVisual,
            quantity = this.quantity,

            isInstanced = this.isInstanced,
            runtimeInstanceId = this.isInstanced ? Guid.NewGuid().ToString() : null,
            rolledRarity = this.rolledRarity,
            rolledModifiers = new List<StatModifier>(),

            gridX = this.gridX,
            gridY = this.gridY,
            rotationIndex = this.rotationIndex,
            rotated = this.rotated
        };

        if (this.rolledModifiers != null)
        {
            foreach (var mod in this.rolledModifiers)
            {
                if (mod == null) continue;
                clone.rolledModifiers.Add(mod.Clone());
            }
        }

        return clone;
    }
}