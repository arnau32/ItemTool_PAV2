using System.Globalization;
using System.Text.RegularExpressions;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.Infrastructure.Unity;

public sealed class UnityContentImporter : IUnityContentImporter
{
    private static readonly Regex GuidRegex = new(
        @"guid:\s*(?<guid>[a-fA-F0-9]+)",
        RegexOptions.Compiled);

    public Task<UnityContentOperationResultDto> ImportAsync(
        string unityProjectRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidUnityProjectRoot(unityProjectRootPath, out string validationMessage))
            return Task.FromResult(UnityContentOperationResultDto.Failure(validationMessage));

        string assetsPath = Path.Combine(unityProjectRootPath, "Assets");

        Dictionary<string, string> guidToAssetPath = BuildGuidToAssetPathMap(
            unityProjectRootPath,
            assetsPath,
            cancellationToken);

        List<ItemDto> importedItems = new();
        int scannedAssetCount = 0;
        int ignoredAssetCount = 0;

        foreach (string assetPath in EnumerateAssetFiles(assetsPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            scannedAssetCount++;

            ItemDto? item = TryImportItemAsset(
                unityProjectRootPath,
                assetPath,
                guidToAssetPath);

            if (item == null)
            {
                ignoredAssetCount++;
                continue;
            }

            importedItems.Add(item);
        }

        ContentDatabaseDto importedDatabase = new()
        {
            SchemaVersion = 1,
            ProjectSettings = new ProjectSettingsDto
            {
                UnityProjectRootPath = unityProjectRootPath
            },
            Items = importedItems,
            LootTables = new List<LootTableDto>()
        };

        UnityContentOperationResultDto result = UnityContentOperationResultDto.Success(
            $"Imported {importedItems.Count} item(s) from Unity. Scanned {scannedAssetCount} .asset file(s), ignored {ignoredAssetCount}.",
            importedDatabase: importedDatabase);

        result.Warnings.Add("Loot table import is not implemented yet. Existing local loot tables will be preserved.");
        result.Warnings.Add("LocalizedString values are not resolved yet. DisplayName falls back to itemNameID or asset name.");
        result.Warnings.Add("Buffs, stat modifiers and complex object references are only partially imported in this first pass.");
        result.Warnings.Add("Some Unity assets have empty m_EditorClassIdentifier, so the importer resolves their type using m_Script guid.");

        return Task.FromResult(result);
    }

    private static ItemDto? TryImportItemAsset(
        string unityProjectRootPath,
        string assetPath,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        string[] lines;

        try
        {
            lines = File.ReadAllLines(assetPath);
        }
        catch
        {
            return null;
        }

        string className = ResolveUnityClassName(lines, guidToAssetPath);

        if (!IsSupportedItemClass(className))
            return null;

        string assetName = ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);
        string uniqueId = ReadScalar(lines, "<uniqueID>k__BackingField") ?? string.Empty;
        string itemNameId = ReadScalar(lines, "itemNameID") ?? uniqueId;

        if (string.IsNullOrWhiteSpace(itemNameId))
            itemNameId = assetName;

        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = itemNameId;

        ItemKind itemKind = GetItemKindFromClassName(
            className,
            ReadInt(lines, "itemType", defaultValue: 0));

        ItemDto item = new()
        {
            Id = uniqueId,
            ItemNameId = itemNameId,
            DisplayName = itemNameId,
            Description = ReadScalar(lines, "description") ?? string.Empty,
            ItemKind = itemKind,
            ItemRarity = ReadEnumInt(lines, "itemRarity", ItemRarity.Common),
            Value = ReadFloat(lines, "value", defaultValue: 0f),
            Weight = ReadFloat(lines, "weight", defaultValue: 0f),
            MaxStack = ReadInt(lines, "maxStack", defaultValue: 1),
            SlotDimension = new DimensionsDto
            {
                Width = ReadNestedInt(lines, "SlotDimension", "Width", defaultValue: 1),
                Height = ReadNestedInt(lines, "SlotDimension", "Height", defaultValue: 1)
            }
        };

        string? iconGuid = ReadObjectGuid(lines, "icon");
        if (!string.IsNullOrWhiteSpace(iconGuid) &&
            guidToAssetPath.TryGetValue(iconGuid, out string iconUnityPath))
        {
            item.IconPath = iconUnityPath;
        }

        item.EnsureDetailsForCurrentKind();

        ApplyTypeSpecificFields(item, lines);

        return item;
    }

    private static string ResolveUnityClassName(
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        string editorClassIdentifier = ReadEditorClassIdentifier(lines);

        if (!string.IsNullOrWhiteSpace(editorClassIdentifier))
            return editorClassIdentifier;

        string? scriptGuid = ReadObjectGuid(lines, "m_Script");

        if (string.IsNullOrWhiteSpace(scriptGuid))
            return string.Empty;

        if (!guidToAssetPath.TryGetValue(scriptGuid, out string scriptUnityPath))
            return string.Empty;

        string scriptFileName = Path.GetFileNameWithoutExtension(scriptUnityPath);

        return scriptFileName?.Trim() ?? string.Empty;
    }

    private static void ApplyTypeSpecificFields(ItemDto item, IReadOnlyList<string> lines)
    {
        if (item.Equipable != null)
        {
            item.Equipable.EquipSlot = ReadEnumInt(
                lines,
                "equipSlot",
                item.ItemKind == ItemKind.Weapon
                    ? EquipSlot.Weapon
                    : EquipSlot.Helmet);

            item.Equipable.Tier = ReadInt(lines, "tier", defaultValue: 0);

            item.Equipable.RollMode = ReadEnumInt(
                lines,
                "rollMode",
                EquipableRollMode.RandomRarityRandomStats);
        }

        if (item.Weapon != null)
        {
            item.Weapon.HandType = ReadEnumInt(
                lines,
                "handType",
                WeaponHandType.Single);

            item.Weapon.FamilyType = ReadEnumInt(
                lines,
                "familyType",
                WeaponFamily.Sword);

            item.Weapon.SkillScoreNeeded = ReadFloat(
                lines,
                "skillScoreNeeded",
                defaultValue: 6f);

            item.Weapon.WeaponTier = ReadInt(
                lines,
                "weaponTier",
                defaultValue: 1);

            item.Weapon.EnemyDamageMultiplier = ReadFloat(
                lines,
                "enemyDamageMultiplier",
                defaultValue: 1f);
        }

        if (item.Collectable != null)
        {
            item.Collectable.CollectionId = ReadInt(
                lines,
                "collectionID",
                defaultValue: 0);

            item.Collectable.IsAuroraDust = ReadBool(
                lines,
                "isAuroraDust",
                defaultValue: false);
        }
    }

    private static Dictionary<string, string> BuildGuidToAssetPathMap(
        string unityProjectRootPath,
        string assetsPath,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(assetsPath))
            return result;

        foreach (string metaPath in EnumerateMetaFiles(assetsPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? guid = TryReadGuidFromMeta(metaPath);

            if (string.IsNullOrWhiteSpace(guid))
                continue;

            string assetPath = metaPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                ? metaPath[..^5]
                : metaPath;

            if (!File.Exists(assetPath))
                continue;

            string unityAssetPath = ToUnityAssetPath(
                unityProjectRootPath,
                assetPath);

            result[guid] = unityAssetPath;
        }

        return result;
    }

    private static string? TryReadGuidFromMeta(string metaPath)
    {
        try
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                string trimmed = line.Trim();

                if (!trimmed.StartsWith("guid:", StringComparison.Ordinal))
                    continue;

                return trimmed["guid:".Length..].Trim();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAssetFiles(string assetsPath)
    {
        try
        {
            return Directory.EnumerateFiles(
                assetsPath,
                "*.asset",
                SearchOption.AllDirectories);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateMetaFiles(string assetsPath)
    {
        try
        {
            return Directory.EnumerateFiles(
                assetsPath,
                "*.meta",
                SearchOption.AllDirectories);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private static string ReadEditorClassIdentifier(IReadOnlyList<string> lines)
    {
        string rawIdentifier = ReadScalar(lines, "m_EditorClassIdentifier") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawIdentifier))
            return string.Empty;

        int separatorIndex = rawIdentifier.LastIndexOf("::", StringComparison.Ordinal);

        if (separatorIndex >= 0 && separatorIndex + 2 < rawIdentifier.Length)
            return rawIdentifier[(separatorIndex + 2)..].Trim();

        return rawIdentifier.Trim();
    }

    private static bool IsSupportedItemClass(string className)
    {
        return className is
            "ItemData" or
            "EquipableItemData" or
            "WeaponData" or
            "ConsumableItemData" or
            "CraftingItemData" or
            "CollectableItemData";
    }

    private static ItemKind GetItemKindFromClassName(
        string className,
        int unityItemType)
    {
        return className switch
        {
            "WeaponData" => ItemKind.Weapon,
            "EquipableItemData" => ItemKind.Equipment,
            "ConsumableItemData" => ItemKind.Consumable,
            "CraftingItemData" => ItemKind.Crafting,
            "CollectableItemData" => ItemKind.Collectable,
            _ => GetItemKindFromUnityItemType(unityItemType)
        };
    }

    private static ItemKind GetItemKindFromUnityItemType(int unityItemType)
    {
        return unityItemType switch
        {
            0 => ItemKind.Equipment,
            1 => ItemKind.Consumable,
            2 => ItemKind.Crafting,
            3 => ItemKind.Collectable,
            _ => ItemKind.Equipment
        };
    }

    private static string? ReadScalar(
        IReadOnlyList<string> lines,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string value = trimmed[prefix.Length..].Trim();

            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value;
        }

        return null;
    }

    private static string? ReadObjectGuid(
        IReadOnlyList<string> lines,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            Match match = GuidRegex.Match(trimmed);

            if (match.Success)
                return match.Groups["guid"].Value;
        }

        return null;
    }

    private static int ReadInt(
        IReadOnlyList<string> lines,
        string fieldName,
        int defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        return int.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
            ? value
            : defaultValue;
    }

    private static float ReadFloat(
        IReadOnlyList<string> lines,
        string fieldName,
        float defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        return float.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float value)
            ? value
            : defaultValue;
    }

    private static bool ReadBool(
        IReadOnlyList<string> lines,
        string fieldName,
        bool defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (raw == "1")
            return true;

        if (raw == "0")
            return false;

        return bool.TryParse(raw, out bool value)
            ? value
            : defaultValue;
    }

    private static int ReadNestedInt(
        IReadOnlyList<string> lines,
        string parentFieldName,
        string childFieldName,
        int defaultValue)
    {
        string parentPrefix = $"{parentFieldName}:";
        string childPrefix = $"{childFieldName}:";

        bool insideParent = false;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith(parentPrefix, StringComparison.Ordinal))
            {
                insideParent = true;
                continue;
            }

            if (!insideParent)
                continue;

            if (!line.StartsWith(" ", StringComparison.Ordinal))
                break;

            if (!trimmed.StartsWith(childPrefix, StringComparison.Ordinal))
                continue;

            string value = trimmed[childPrefix.Length..].Trim();

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                ? parsed
                : defaultValue;
        }

        return defaultValue;
    }

    private static TEnum ReadEnumInt<TEnum>(
        IReadOnlyList<string> lines,
        string fieldName,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        int rawValue = ReadInt(
            lines,
            fieldName,
            Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture));

        if (Enum.IsDefined(typeof(TEnum), rawValue))
            return (TEnum)Enum.ToObject(typeof(TEnum), rawValue);

        return defaultValue;
    }

    private static string ToUnityAssetPath(
        string unityProjectRootPath,
        string absoluteAssetPath)
    {
        string relativePath = Path.GetRelativePath(
            unityProjectRootPath,
            absoluteAssetPath);

        return relativePath.Replace('\\', '/');
    }

    private static bool IsValidUnityProjectRoot(
        string unityProjectRootPath,
        out string validationMessage)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRootPath))
        {
            validationMessage = "Unity project root path is empty.";
            return false;
        }

        if (!Directory.Exists(unityProjectRootPath))
        {
            validationMessage = "Unity project root folder does not exist.";
            return false;
        }

        string assetsPath = Path.Combine(unityProjectRootPath, "Assets");
        string projectSettingsPath = Path.Combine(unityProjectRootPath, "ProjectSettings");

        if (!Directory.Exists(assetsPath))
        {
            validationMessage = "Selected folder does not contain an Assets folder.";
            return false;
        }

        if (!Directory.Exists(projectSettingsPath))
        {
            validationMessage = "Selected folder does not contain a ProjectSettings folder.";
            return false;
        }

        validationMessage = "Unity project root is valid.";
        return true;
    }
}