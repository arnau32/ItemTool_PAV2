using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.Services;

public sealed class ItemFactory
{
    public ItemDto Create(ItemKind kind)
    {
        string baseId = GenerateDefaultId(kind);

        ItemDto item = new()
        {
            Id = baseId,
            ItemKind = kind,
            ItemType = ItemDto.GetItemTypeFromKind(kind),
            ItemNameId = baseId,
            DisplayName = GenerateDefaultDisplayName(kind),
            Description = string.Empty,
            IconPath = null,
            ItemRarity = ItemRarity.Common,
            Value = 0,
            Weight = 0,
            MaxStack = GetDefaultMaxStack(kind),
            SlotDimension = GetDefaultSlotDimension(kind)
        };

        item.EnsureDetailsForCurrentKind();
        ApplyKindDefaults(item);

        return item;
    }

    private static string GenerateDefaultId(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Equipment => "new_equipment_item",
            ItemKind.Weapon => "new_weapon_item",
            ItemKind.Consumable => "new_consumable_item",
            ItemKind.Crafting => "new_crafting_item",
            ItemKind.Collectable => "new_collectable_item",
            _ => "new_item"
        };
    }

    private static string GenerateDefaultDisplayName(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Equipment => "New Equipment Item",
            ItemKind.Weapon => "New Weapon Item",
            ItemKind.Consumable => "New Consumable Item",
            ItemKind.Crafting => "New Crafting Item",
            ItemKind.Collectable => "New Collectable Item",
            _ => "New Item"
        };
    }

    private static int GetDefaultMaxStack(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Equipment => 1,
            ItemKind.Weapon => 1,
            _ => 1
        };
    }

    private static DimensionsDto GetDefaultSlotDimension(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Weapon => new DimensionsDto
            {
                Width = 1,
                Height = 3
            },

            _ => new DimensionsDto
            {
                Width = 1,
                Height = 1
            }
        };
    }

    private static void ApplyKindDefaults(ItemDto item)
    {
        switch (item.ItemKind)
        {
            case ItemKind.Equipment:
                if (item.Equipable != null)
                {
                    item.Equipable.EquipSlot = EquipSlot.Backpack;
                    item.Equipable.Tier = 0;
                    item.Equipable.RollMode = EquipableRollMode.RandomRarityRandomStats;
                }
                break;

            case ItemKind.Weapon:
                if (item.Equipable != null)
                {
                    item.Equipable.EquipSlot = EquipSlot.Weapon;
                    item.Equipable.Tier = 0;
                    item.Equipable.RollMode = EquipableRollMode.RandomRarityRandomStats;
                }

                if (item.Weapon != null)
                {
                    item.Weapon.WeaponTier = 1;
                    item.Weapon.EnemyDamageMultiplier = 1f;
                    item.Weapon.SkillScoreNeeded = 6f;
                }
                break;

            case ItemKind.Consumable:
            case ItemKind.Crafting:
            case ItemKind.Collectable:
                break;
        }
    }
}