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

        List<string> assetPaths = EnumerateAssetFiles(assetsPath).ToList();

        Dictionary<string, string> itemIdByUnityPath = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> lootTableIdByUnityPath = new(StringComparer.OrdinalIgnoreCase);

        BuildReferenceIdMaps(
            unityProjectRootPath,
            assetPaths,
            guidToAssetPath,
            itemIdByUnityPath,
            lootTableIdByUnityPath,
            cancellationToken);

        List<ItemDto> importedItems = new();
        List<LootTableDto> importedLootTables = new();

        int scannedAssetCount = 0;
        int ignoredAssetCount = 0;

        foreach (string assetPath in assetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            scannedAssetCount++;

            string[]? lines = TryReadAllLines(assetPath);
            if (lines == null)
            {
                ignoredAssetCount++;
                continue;
            }

            string className = ResolveUnityClassName(lines, guidToAssetPath);

            if (IsSupportedItemClass(className))
            {
                ItemDto? item = TryImportItemAsset(
                    unityProjectRootPath,
                    assetPath,
                    lines,
                    className,
                    guidToAssetPath);

                if (item != null)
                {
                    importedItems.Add(item);
                    continue;
                }
            }

            if (IsLootTableClass(className))
            {
                LootTableDto? lootTable = TryImportLootTableAsset(
                    unityProjectRootPath,
                    assetPath,
                    lines,
                    guidToAssetPath,
                    itemIdByUnityPath,
                    lootTableIdByUnityPath);

                if (lootTable != null)
                {
                    importedLootTables.Add(lootTable);
                    continue;
                }
            }

            ignoredAssetCount++;
        }

        ContentDatabaseDto importedDatabase = new()
        {
            SchemaVersion = 1,
            ProjectSettings = new ProjectSettingsDto
            {
                UnityProjectRootPath = unityProjectRootPath
            },
            Items = importedItems,
            LootTables = importedLootTables
        };

        UnityContentOperationResultDto result = UnityContentOperationResultDto.Success(
            $"Imported {importedItems.Count} item(s) and {importedLootTables.Count} loot table(s) from Unity. Scanned {scannedAssetCount} .asset file(s), ignored {ignoredAssetCount}.",
            importedDatabase: importedDatabase);

        result.Warnings.Add("LocalizedString values are not resolved yet. DisplayName falls back to itemNameID or asset name.");
        result.Warnings.Add("Buffs, stat modifiers and complex object references are only partially imported in this pass.");
        result.Warnings.Add("Some Unity assets have empty m_EditorClassIdentifier, so the importer resolves their type using m_Script guid.");
        result.Warnings.Add("Loot table references are resolved by GUID. Missing or broken references become empty IDs and will be reported by validation.");

        return Task.FromResult(result);
    }

    private static void BuildReferenceIdMaps(
        string unityProjectRootPath,
        IEnumerable<string> assetPaths,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        Dictionary<string, string> itemIdByUnityPath,
        Dictionary<string, string> lootTableIdByUnityPath,
        CancellationToken cancellationToken)
    {
        foreach (string assetPath in assetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[]? lines = TryReadAllLines(assetPath);
            if (lines == null)
                continue;

            string className = ResolveUnityClassName(lines, guidToAssetPath);
            string unityPath = ToUnityAssetPath(unityProjectRootPath, assetPath);
            string assetName = ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);

            if (IsSupportedItemClass(className))
            {
                string uniqueId = ReadScalar(lines, "<uniqueID>k__BackingField") ?? string.Empty;
                string itemNameId = ReadScalar(lines, "itemNameID") ?? uniqueId;

                if (string.IsNullOrWhiteSpace(itemNameId))
                    itemNameId = assetName;

                if (string.IsNullOrWhiteSpace(uniqueId))
                    uniqueId = itemNameId;

                itemIdByUnityPath[unityPath] = uniqueId;
                continue;
            }

            if (IsLootTableClass(className))
            {
                string lootTableId = ReadScalar(lines, "lootTableID") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(lootTableId))
                    lootTableId = assetName;

                lootTableIdByUnityPath[unityPath] = lootTableId;
            }
        }
    }

    private static ItemDto? TryImportItemAsset(
        string unityProjectRootPath,
        string assetPath,
        IReadOnlyList<string> lines,
        string className,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
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

        ApplyItemTypeSpecificFields(item, lines);

        return item;
    }

    private static LootTableDto? TryImportLootTableAsset(
        string unityProjectRootPath,
        string assetPath,
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        IReadOnlyDictionary<string, string> itemIdByUnityPath,
        IReadOnlyDictionary<string, string> lootTableIdByUnityPath)
    {
        string assetName = ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);
        string lootTableId = ReadScalar(lines, "lootTableID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lootTableId))
            lootTableId = assetName;

        LootTableDto lootTable = new()
        {
            Id = lootTableId,
            Name = assetName,
            MinRandomPicks = ReadInt(lines, "minRandomPicks", defaultValue: 0),
            MaxRandomPicks = ReadInt(lines, "maxRandomPicks", defaultValue: 0)
        };

        foreach (LootEntryDto entry in ReadLootEntries(
                     unityProjectRootPath,
                     lines,
                     "guaranteedEntries",
                     requiresWeight: false,
                     guidToAssetPath,
                     itemIdByUnityPath,
                     lootTableIdByUnityPath))
        {
            lootTable.GuaranteedEntries.Add(entry);
        }

        foreach (LootEntryDto entry in ReadLootEntries(
                     unityProjectRootPath,
                     lines,
                     "weightedEntries",
                     requiresWeight: true,
                     guidToAssetPath,
                     itemIdByUnityPath,
                     lootTableIdByUnityPath))
        {
            lootTable.WeightedEntries.Add(entry);
        }

        return lootTable;
    }

    private static IEnumerable<LootEntryDto> ReadLootEntries(
        string unityProjectRootPath,
        IReadOnlyList<string> lines,
        string listFieldName,
        bool requiresWeight,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        IReadOnlyDictionary<string, string> itemIdByUnityPath,
        IReadOnlyDictionary<string, string> lootTableIdByUnityPath)
    {
        List<List<string>> entryBlocks = ReadYamlListBlocks(lines, listFieldName);

        foreach (List<string> entryBlock in entryBlocks)
        {
            LootEntryType entryType = ReadEnumIntFromBlock(
                entryBlock,
                "entryType",
                LootEntryType.Item);

            LootEntryDto entry = new()
            {
                EntryType = entryType,
                MinQuantity = ReadIntFromBlock(entryBlock, "minQuantity", defaultValue: 1),
                MaxQuantity = ReadIntFromBlock(entryBlock, "maxQuantity", defaultValue: 1),
                Weight = ReadIntFromBlock(entryBlock, "weight", defaultValue: requiresWeight ? 1 : 0),
                EquipableOverrideMode = ReadEnumIntFromBlock(
                    entryBlock,
                    "equipableOverrideMode",
                    LootEntryEquipableOverrideMode.None),
                OverrideFixedRarity = ReadEnumIntFromBlock(
                    entryBlock,
                    "overrideFixedRarity",
                    ItemRarity.Common)
            };

            if (entry.EntryType == LootEntryType.Item)
            {
                string? itemGuid = ReadObjectGuidFromBlock(entryBlock, "item");

                if (!string.IsNullOrWhiteSpace(itemGuid) &&
                    TryResolveReferencedId(
                        unityProjectRootPath,
                        itemGuid,
                        guidToAssetPath,
                        itemIdByUnityPath,
                        out string itemId))
                {
                    entry.ItemId = itemId;
                }
            }
            else if (entry.EntryType == LootEntryType.LootTable)
            {
                string? nestedTableGuid = ReadObjectGuidFromBlock(entryBlock, "nestedTable");

                if (!string.IsNullOrWhiteSpace(nestedTableGuid) &&
                    TryResolveReferencedId(
                        unityProjectRootPath,
                        nestedTableGuid,
                        guidToAssetPath,
                        lootTableIdByUnityPath,
                        out string lootTableId))
                {
                    entry.NestedLootTableId = lootTableId;
                }
            }

            yield return entry;
        }
    }

    private static bool TryResolveReferencedId(
        string unityProjectRootPath,
        string guid,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        IReadOnlyDictionary<string, string> idByUnityPath,
        out string id)
    {
        id = string.Empty;

        if (!guidToAssetPath.TryGetValue(guid, out string unityAssetPath))
            return false;

        if (idByUnityPath.TryGetValue(unityAssetPath, out string resolvedId))
        {
            id = resolvedId;
            return true;
        }

        string normalizedUnityPath = unityAssetPath.Replace('\\', '/');

        if (idByUnityPath.TryGetValue(normalizedUnityPath, out resolvedId))
        {
            id = resolvedId;
            return true;
        }

        string absoluteCandidate = Path.Combine(
            unityProjectRootPath,
            unityAssetPath.Replace('/', Path.DirectorySeparatorChar));

        string candidateUnityPath = ToUnityAssetPath(
            unityProjectRootPath,
            absoluteCandidate);

        if (idByUnityPath.TryGetValue(candidateUnityPath, out resolvedId))
        {
            id = resolvedId;
            return true;
        }

        return false;
    }

    private static void ApplyItemTypeSpecificFields(ItemDto item, IReadOnlyList<string> lines)
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

    private static bool IsLootTableClass(string className)
    {
        return string.Equals(className, "LootTable", StringComparison.Ordinal);
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

    private static string[]? TryReadAllLines(string path)
    {
        try
        {
            return File.ReadAllLines(path);
        }
        catch
        {
            return null;
        }
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

    private static List<List<string>> ReadYamlListBlocks(
        IReadOnlyList<string> lines,
        string listFieldName)
    {
        List<List<string>> blocks = new();

        string listPrefix = $"{listFieldName}:";
        bool insideList = false;
        List<string>? currentBlock = null;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!insideList)
            {
                if (trimmed.StartsWith(listPrefix, StringComparison.Ordinal))
                    insideList = true;

                continue;
            }

            if (!line.StartsWith(" ", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (currentBlock != null && currentBlock.Count > 0)
                    blocks.Add(currentBlock);

                currentBlock = new List<string>
                {
                    trimmed[2..]
                };

                continue;
            }

            currentBlock?.Add(trimmed);
        }

        if (currentBlock != null && currentBlock.Count > 0)
            blocks.Add(currentBlock);

        return blocks;
    }

    private static string? ReadScalarFromBlock(
        IReadOnlyList<string> block,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in block)
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

    private static string? ReadObjectGuidFromBlock(
        IReadOnlyList<string> block,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in block)
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

    private static int ReadIntFromBlock(
        IReadOnlyList<string> block,
        string fieldName,
        int defaultValue)
    {
        string? raw = ReadScalarFromBlock(block, fieldName);

        return int.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
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

    private static TEnum ReadEnumIntFromBlock<TEnum>(
        IReadOnlyList<string> block,
        string fieldName,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        int rawValue = ReadIntFromBlock(
            block,
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