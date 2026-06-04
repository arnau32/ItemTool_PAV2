using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.Application.Validation;

public sealed class ItemValidator
{
    public IReadOnlyList<ValidationIssue> Validate(ItemDto item, IEnumerable<ItemDto> allItems)
    {
        List<ValidationIssue> issues = new();

        if (item == null)
        {
            issues.Add(Error("Item inválido o null."));
            return issues;
        }

        ValidateIdentity(item, allItems, issues);
        ValidateGeneralData(item, issues);
        ValidateItemKindConsistency(item, issues);
        ValidateTypeSpecificData(item, issues);

        return issues;
    }

    private static void ValidateIdentity(
        ItemDto item,
        IEnumerable<ItemDto> allItems,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            issues.Add(Error("Id es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(item.ItemNameId))
        {
            issues.Add(Error("Item Name ID es obligatorio."));
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            int duplicatedIds = allItems.Count(x =>
                !ReferenceEquals(x, item) &&
                !string.IsNullOrWhiteSpace(x.Id) &&
                string.Equals(x.Id.Trim(), item.Id.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicatedIds > 0)
                issues.Add(Error($"Id duplicado: \"{item.Id}\"."));
        }

        if (!string.IsNullOrWhiteSpace(item.ItemNameId))
        {
            int duplicatedNameIds = allItems.Count(x =>
                !ReferenceEquals(x, item) &&
                !string.IsNullOrWhiteSpace(x.ItemNameId) &&
                string.Equals(x.ItemNameId.Trim(), item.ItemNameId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicatedNameIds > 0)
                issues.Add(Error($"Item Name ID duplicado: \"{item.ItemNameId}\"."));
        }
    }

    private static void ValidateGeneralData(ItemDto item, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(item.DisplayName))
        {
            issues.Add(Warning("Display Name está vacío. El item será difícil de identificar para diseñadores/jugadores."));
        }

        if (string.IsNullOrWhiteSpace(item.IconPath))
        {
            issues.Add(Warning("Icon Path está vacío. El item aparecerá sin preview visual."));
        }

        if (item.Value < 0)
        {
            issues.Add(Error("Value debe ser >= 0."));
        }

        if (item.Weight < 0)
        {
            issues.Add(Error("Weight debe ser >= 0."));
        }

        if (item.MaxStack < 1)
        {
            issues.Add(Error("Max Stack debe ser >= 1."));
        }

        if (item.SlotDimension == null)
        {
            issues.Add(Error("Slot Dimension es obligatorio."));
            return;
        }

        if (item.SlotDimension.Width < 1)
        {
            issues.Add(Error("Width debe ser >= 1."));
        }

        if (item.SlotDimension.Height < 1)
        {
            issues.Add(Error("Height debe ser >= 1."));
        }
    }

    private static void ValidateItemKindConsistency(ItemDto item, List<ValidationIssue> issues)
    {
        ItemType expectedItemType = ItemDto.GetItemTypeFromKind(item.ItemKind);

        if (item.ItemType != expectedItemType)
        {
            issues.Add(Error(
                $"ItemType incoherente. ItemKind '{item.ItemKind}' debería usar ItemType '{expectedItemType}', pero tiene '{item.ItemType}'."));
        }
    }

    private static void ValidateTypeSpecificData(ItemDto item, List<ValidationIssue> issues)
    {
        switch (item.ItemKind)
        {
            case ItemKind.Equipment:
                ValidateEquipment(item, issues);
                break;

            case ItemKind.Weapon:
                ValidateWeapon(item, issues);
                break;

            case ItemKind.Consumable:
                ValidateConsumable(item, issues);
                break;

            case ItemKind.Crafting:
                ValidateCrafting(item, issues);
                break;

            case ItemKind.Collectable:
                ValidateCollectable(item, issues);
                break;

            default:
                issues.Add(Error($"ItemKind no soportado: {item.ItemKind}."));
                break;
        }
    }

    private static void ValidateEquipment(ItemDto item, List<ValidationIssue> issues)
    {
        if (item.Equipable == null)
        {
            issues.Add(Error("Equipment debe tener datos Equipable."));
            return;
        }

        if (item.Weapon != null)
        {
            issues.Add(Warning("Equipment tiene datos Weapon asignados, pero no se usarán."));
        }

        if (item.Consumable != null || item.Collectable != null)
        {
            issues.Add(Warning("Equipment tiene datos de otro tipo asignados. Se recomienda limpiar datos incompatibles."));
        }

        if (item.Equipable.Tier < 0)
        {
            issues.Add(Error("Equipable Tier debe ser >= 0."));
        }

        if (item.Equipable.Modifiers == null || item.Equipable.Modifiers.Count == 0)
        {
            issues.Add(Warning("Equipable sin Stat Modifiers configurados."));
        }
        else
        {
            ValidateStatModifiers(item.Equipable.Modifiers, issues);
        }
    }

    private static void ValidateWeapon(ItemDto item, List<ValidationIssue> issues)
    {
        if (item.Equipable == null)
        {
            issues.Add(Error("Weapon debe tener datos Equipable porque WeaponData hereda de EquipableItemData."));
        }
        else
        {
            if (item.Equipable.EquipSlot != EquipSlot.Weapon)
            {
                issues.Add(Warning("Weapon debería tener EquipSlot = Weapon."));
            }

            if (item.Equipable.Tier < 0)
            {
                issues.Add(Error("Equipable Tier debe ser >= 0."));
            }

            if (item.Equipable.Modifiers == null || item.Equipable.Modifiers.Count == 0)
            {
                issues.Add(Warning("Weapon sin Stat Modifiers configurados."));
            }
            else
            {
                ValidateStatModifiers(item.Equipable.Modifiers, issues);
            }
        }

        if (item.Weapon == null)
        {
            issues.Add(Error("Weapon debe tener datos Weapon."));
            return;
        }

        if (item.Weapon.WeaponTier < 1 || item.Weapon.WeaponTier > 4)
        {
            issues.Add(Error("Weapon Tier debe estar entre 1 y 4."));
        }

        if (item.Weapon.EnemyDamageMultiplier < 0)
        {
            issues.Add(Error("Enemy Damage Multiplier debe ser >= 0."));
        }

        if (item.Weapon.SkillScoreNeeded < 0)
        {
            issues.Add(Error("Skill Score Needed debe ser >= 0."));
        }

        if (item.Consumable != null || item.Collectable != null)
        {
            issues.Add(Warning("Weapon tiene datos de otro tipo asignados. Se recomienda limpiar datos incompatibles."));
        }
    }

    private static void ValidateConsumable(ItemDto item, List<ValidationIssue> issues)
    {
        if (item.Consumable == null)
        {
            issues.Add(Error("Consumable debe tener datos Consumable."));
            return;
        }

        if (item.Equipable != null || item.Weapon != null || item.Collectable != null)
        {
            issues.Add(Warning("Consumable tiene datos de otro tipo asignados. Se recomienda limpiar datos incompatibles."));
        }

        if (item.Consumable.Buffs == null || item.Consumable.Buffs.Count == 0)
        {
            issues.Add(Warning("Consumable sin Buffs configurados."));
            return;
        }

        foreach (BuffEffectDto buff in item.Consumable.Buffs)
        {
            if (buff.Duration < 0)
            {
                issues.Add(Error("Buff Duration debe ser >= 0."));
            }

            if (buff.Value == 0)
            {
                issues.Add(Warning("Buff con Value = 0. Puede no tener efecto real."));
            }
        }
    }

    private static void ValidateCrafting(ItemDto item, List<ValidationIssue> issues)
    {
        if (item.Equipable != null || item.Weapon != null || item.Consumable != null || item.Collectable != null)
        {
            issues.Add(Warning("Crafting tiene datos específicos de otro tipo. Se recomienda limpiar datos incompatibles."));
        }
    }

    private static void ValidateCollectable(ItemDto item, List<ValidationIssue> issues)
    {
        if (item.Collectable == null)
        {
            issues.Add(Error("Collectable debe tener datos Collectable."));
            return;
        }

        if (item.Equipable != null || item.Weapon != null || item.Consumable != null)
        {
            issues.Add(Warning("Collectable tiene datos de otro tipo asignados. Se recomienda limpiar datos incompatibles."));
        }

        if (item.Collectable.CollectionId < 0)
        {
            issues.Add(Error("Collection ID debe ser >= 0."));
        }
    }

    private static void ValidateStatModifiers(
        IEnumerable<StatModifierDto> modifiers,
        List<ValidationIssue> issues)
    {
        foreach (StatModifierDto modifier in modifiers)
        {
            if (modifier.Value == 0)
            {
                issues.Add(Warning($"Stat Modifier '{modifier.StatType}' tiene Value = 0."));
            }
        }
    }

    private static ValidationIssue Error(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Message = message
        };
    }

    private static ValidationIssue Warning(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Message = message
        };
    }

    private static ValidationIssue Info(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Info,
            Message = message
        };
    }
}