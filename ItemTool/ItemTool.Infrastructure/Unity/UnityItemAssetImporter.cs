using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.Infrastructure.Unity;

internal static class UnityItemAssetImporter
{
    public static ItemDto? TryImport(
        string assetPath,
        IReadOnlyList<string> lines,
        string className,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        string assetName = UnityYamlReader.ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);
        string uniqueId = UnityYamlReader.ReadScalar(lines, "<uniqueID>k__BackingField") ?? string.Empty;
        string itemNameId = UnityYamlReader.ReadScalar(lines, "itemNameID") ?? uniqueId;

        if (string.IsNullOrWhiteSpace(itemNameId))
            itemNameId = assetName;

        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = itemNameId;

        ItemKind itemKind = UnityClassResolver.GetItemKindFromClassName(
            className,
            UnityYamlReader.ReadInt(lines, "itemType", defaultValue: 0));

        ItemDto item = new()
        {
            Id = uniqueId,
            ItemNameId = itemNameId,
            DisplayName = itemNameId,
            Description = UnityYamlReader.ReadScalar(lines, "description") ?? string.Empty,
            ItemKind = itemKind,
            ItemRarity = UnityYamlReader.ReadEnumInt(lines, "itemRarity", ItemRarity.Common),
            Value = UnityYamlReader.ReadFloat(lines, "value", defaultValue: 0f),
            Weight = UnityYamlReader.ReadFloat(lines, "weight", defaultValue: 0f),
            MaxStack = UnityYamlReader.ReadInt(lines, "maxStack", defaultValue: 1),
            SlotDimension = new DimensionsDto
            {
                Width = UnityYamlReader.ReadNestedInt(lines, "SlotDimension", "Width", defaultValue: 1),
                Height = UnityYamlReader.ReadNestedInt(lines, "SlotDimension", "Height", defaultValue: 1)
            }
        };

        string? iconGuid = UnityYamlReader.ReadObjectGuid(lines, "icon");
        if (!string.IsNullOrWhiteSpace(iconGuid) &&
            guidToAssetPath.TryGetValue(iconGuid, out string iconUnityPath))
        {
            item.IconPath = iconUnityPath;
        }

        item.EnsureDetailsForCurrentKind();

        ApplyTypeSpecificFields(item, lines, guidToAssetPath);

        return item;
    }

    private static void ApplyTypeSpecificFields(
        ItemDto item,
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        if (item.Equipable != null)
        {
            item.Equipable.EquipSlot = UnityYamlReader.ReadEnumInt(
                lines,
                "equipSlot",
                item.ItemKind == ItemKind.Weapon
                    ? EquipSlot.Weapon
                    : EquipSlot.Helmet);

            item.Equipable.Tier = UnityYamlReader.ReadInt(lines, "tier", defaultValue: 0);

            item.Equipable.RollMode = UnityYamlReader.ReadEnumInt(
                lines,
                "rollMode",
                EquipableRollMode.RandomRarityRandomStats);

            item.Equipable.PrefabPath = ResolveUnityObjectPath(
                lines,
                "prefab",
                guidToAssetPath);

            ImportStatModifiers(item.Equipable, lines);
        }

        if (item.Weapon != null)
        {
            item.Weapon.HandType = UnityYamlReader.ReadEnumInt(
                lines,
                "handType",
                WeaponHandType.Single);

            item.Weapon.FamilyType = UnityYamlReader.ReadEnumInt(
                lines,
                "familyType",
                WeaponFamily.Sword);

            item.Weapon.SkillScoreNeeded = UnityYamlReader.ReadFloat(
                lines,
                "skillScoreNeeded",
                defaultValue: 6f);

            item.Weapon.WeaponTier = UnityYamlReader.ReadInt(
                lines,
                "weaponTier",
                defaultValue: 1);

            item.Weapon.EnemyDamageMultiplier = UnityYamlReader.ReadFloat(
                lines,
                "enemyDamageMultiplier",
                defaultValue: 1f);

            item.Weapon.PrefabVariantPath = ResolveUnityObjectPath(
                lines,
                "prefabVariant",
                guidToAssetPath);
        }

        if (item.Consumable != null)
        {
            ImportBuffEffects(item.Consumable, lines);
        }

        if (item.Collectable != null)
        {
            item.Collectable.CollectionId = UnityYamlReader.ReadInt(
                lines,
                "collectionID",
                defaultValue: 0);

            item.Collectable.IsAuroraDust = UnityYamlReader.ReadBool(
                lines,
                "isAuroraDust",
                defaultValue: false);
        }
    }

    private static string ResolveUnityObjectPath(
        IReadOnlyList<string> lines,
        string fieldName,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        string? guid = UnityYamlReader.ReadObjectGuid(lines, fieldName);

        if (string.IsNullOrWhiteSpace(guid))
            return string.Empty;

        return guidToAssetPath.TryGetValue(guid, out string unityPath)
            ? unityPath
            : string.Empty;
    }

    private static void ImportStatModifiers(
        EquipableItemDetailsDto equipable,
        IReadOnlyList<string> lines)
    {
        List<List<string>> modifierBlocks = UnityYamlReader.ReadYamlListBlocks(
            lines,
            "modifiers");

        equipable.Modifiers.Clear();

        foreach (List<string> modifierBlock in modifierBlocks)
        {
            StatModifierDto modifier = new()
            {
                StatType = UnityYamlReader.ReadEnumIntFromBlock(
                    modifierBlock,
                    "statTypeAffected",
                    StatType.Attack),

                Value = UnityYamlReader.ReadFloatFromBlock(
                    modifierBlock,
                    "value",
                    defaultValue: 0f)
            };

            equipable.Modifiers.Add(modifier);
        }
    }

    private static void ImportBuffEffects(
        ConsumableItemDetailsDto consumable,
        IReadOnlyList<string> lines)
    {
        List<List<string>> buffBlocks = UnityYamlReader.ReadYamlListBlocks(
            lines,
            "buffs");

        consumable.Buffs.Clear();

        foreach (List<string> buffBlock in buffBlocks)
        {
            BuffEffectDto buff = new()
            {
                ApplicationMode = UnityYamlReader.ReadEnumIntFromBlock(
                    buffBlock,
                    "applicationMode",
                    BuffApplicationMode.InstantHeal),

                StatType = UnityYamlReader.ReadEnumIntFromBlock(
                    buffBlock,
                    "statType",
                    StatType.Health),

                Value = UnityYamlReader.ReadFloatFromBlock(
                    buffBlock,
                    "baseValue",
                    defaultValue: 0f),

                Duration = UnityYamlReader.ReadFloatFromBlock(
                    buffBlock,
                    "duration",
                    defaultValue: 0f)
            };

            consumable.Buffs.Add(buff);
        }
    }
}