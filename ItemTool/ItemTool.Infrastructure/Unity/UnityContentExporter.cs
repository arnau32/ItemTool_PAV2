using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.Infrastructure.Unity;

public sealed class UnityContentExporter : IUnityContentExporter
{
    private readonly JsonSerializerOptions _jsonOptions;

    public UnityContentExporter()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<UnityContentOperationResultDto> ExportAsync(
        ContentDatabaseDto database,
        string unityProjectRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidUnityProjectRoot(unityProjectRootPath, out string validationMessage))
            return UnityContentOperationResultDto.Failure(validationMessage);

        int updatedItems = 0;
        int updatedLootTables = 0;
        int skippedItems = 0;
        int skippedLootTables = 0;
        int unchangedAssets = 0;

        List<string> warnings = new();

        Dictionary<string, string> itemGuidById = BuildUnityGuidByIdMap(
            database.Items,
            unityProjectRootPath,
            x => x.Id,
            x => x.SourceAssetPath,
            "Item",
            warnings);

        Dictionary<string, string> lootTableGuidById = BuildUnityGuidByIdMap(
            database.LootTables,
            unityProjectRootPath,
            x => x.Id,
            x => x.SourceAssetPath,
            "Loot table",
            warnings);

        foreach (ItemDto item in database.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExportAssetResult result = await ExportItemAsync(
                item,
                unityProjectRootPath,
                cancellationToken);

            switch (result.Status)
            {
                case ExportAssetStatus.Updated:
                    updatedItems++;
                    break;

                case ExportAssetStatus.Unchanged:
                    unchangedAssets++;
                    break;

                case ExportAssetStatus.Skipped:
                    skippedItems++;
                    AddWarning(warnings, result.Message);
                    break;
            }
        }

        foreach (LootTableDto lootTable in database.LootTables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExportAssetResult result = await ExportLootTableAsync(
                lootTable,
                unityProjectRootPath,
                itemGuidById,
                lootTableGuidById,
                warnings,
                cancellationToken);

            switch (result.Status)
            {
                case ExportAssetStatus.Updated:
                    updatedLootTables++;
                    break;

                case ExportAssetStatus.Unchanged:
                    unchangedAssets++;
                    break;

                case ExportAssetStatus.Skipped:
                    skippedLootTables++;
                    AddWarning(warnings, result.Message);
                    break;
            }
        }

        string previewPath = await WritePreviewJsonAsync(
            database,
            unityProjectRootPath,
            cancellationToken);

        UnityContentOperationResultDto operationResult = UnityContentOperationResultDto.Success(
            $"Unity export completed. Updated {updatedItems} item asset(s) and {updatedLootTables} loot table asset(s). Skipped {skippedItems} item(s), {skippedLootTables} loot table(s), {unchangedAssets} unchanged asset(s).",
            previewPath);

        foreach (string warning in warnings)
            operationResult.Warnings.Add(warning);

        operationResult.Warnings.Add("Export phase 3 writes scalar fields, stat modifiers, buff effects and loot table entries.");
        operationResult.Warnings.Add("Icon refs and prefab refs are still not exported.");
        operationResult.Warnings.Add("A timestamped .bak file is created before each modified .asset is overwritten.");

        return operationResult;
    }

    private static async Task<ExportAssetResult> ExportItemAsync(
        ItemDto item,
        string unityProjectRootPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.SourceAssetPath))
            return ExportAssetResult.Skipped($"Item \"{item.Id}\" has no SourceAssetPath.");

        string? absoluteAssetPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            item.SourceAssetPath);

        if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            return ExportAssetResult.Skipped($"Item \"{item.Id}\" source asset not found: {item.SourceAssetPath}");

        List<string> lines = (await File.ReadAllLinesAsync(
                absoluteAssetPath,
                cancellationToken))
            .ToList();

        bool changed = false;

        changed |= SetScalar(lines, "<uniqueID>k__BackingField", item.Id);
        changed |= SetScalar(lines, "itemType", FormatInt(GetUnityItemType(item)));
        changed |= SetScalar(lines, "itemNameID", item.ItemNameId);
        changed |= SetScalar(lines, "itemRarity", FormatEnum(item.ItemRarity));
        changed |= SetScalar(lines, "value", FormatFloat(item.Value));
        changed |= SetScalar(lines, "weight", FormatFloat(item.Weight));
        changed |= SetScalar(lines, "maxStack", FormatInt(item.MaxStack));
        changed |= SetNestedScalar(lines, "SlotDimension", "Height", FormatInt(item.SlotDimension.Height));
        changed |= SetNestedScalar(lines, "SlotDimension", "Width", FormatInt(item.SlotDimension.Width));

        if (item.Equipable != null)
        {
            changed |= SetScalar(lines, "equipSlot", FormatEnum(item.Equipable.EquipSlot));
            changed |= SetScalar(lines, "tier", FormatInt(item.Equipable.Tier));
            changed |= SetScalar(lines, "rollMode", FormatEnum(item.Equipable.RollMode));
            changed |= SetYamlListBlocks(
                lines,
                "modifiers",
                item.Equipable.Modifiers.Select(BuildStatModifierBlock).ToList());
        }

        if (item.Weapon != null)
        {
            changed |= SetScalar(lines, "handType", FormatEnum(item.Weapon.HandType));
            changed |= SetScalar(lines, "familyType", FormatEnum(item.Weapon.FamilyType));
            changed |= SetScalar(lines, "skillScoreNeeded", FormatFloat(item.Weapon.SkillScoreNeeded));
            changed |= SetScalar(lines, "weaponTier", FormatInt(item.Weapon.WeaponTier));
            changed |= SetScalar(lines, "enemyDamageMultiplier", FormatFloat(item.Weapon.EnemyDamageMultiplier));
        }

        if (item.Consumable != null)
        {
            changed |= SetYamlListBlocks(
                lines,
                "buffs",
                item.Consumable.Buffs.Select(BuildBuffEffectBlock).ToList());
        }

        if (item.Collectable != null)
        {
            changed |= SetScalar(lines, "collectionID", FormatInt(item.Collectable.CollectionId));
            changed |= SetScalar(lines, "isAuroraDust", FormatBool(item.Collectable.IsAuroraDust));
        }

        if (!changed)
            return ExportAssetResult.Unchanged();

        CreateBackup(absoluteAssetPath);

        await File.WriteAllLinesAsync(
            absoluteAssetPath,
            lines,
            cancellationToken);

        return ExportAssetResult.Updated();
    }

    private static async Task<ExportAssetResult> ExportLootTableAsync(
        LootTableDto lootTable,
        string unityProjectRootPath,
        IReadOnlyDictionary<string, string> itemGuidById,
        IReadOnlyDictionary<string, string> lootTableGuidById,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lootTable.SourceAssetPath))
            return ExportAssetResult.Skipped($"Loot table \"{lootTable.Id}\" has no SourceAssetPath.");

        string? absoluteAssetPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            lootTable.SourceAssetPath);

        if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            return ExportAssetResult.Skipped($"Loot table \"{lootTable.Id}\" source asset not found: {lootTable.SourceAssetPath}");

        List<string> lines = (await File.ReadAllLinesAsync(
                absoluteAssetPath,
                cancellationToken))
            .ToList();

        bool changed = false;

        changed |= SetScalar(lines, "lootTableID", lootTable.Id);
        changed |= SetScalar(lines, "minRandomPicks", FormatInt(lootTable.MinRandomPicks));
        changed |= SetScalar(lines, "maxRandomPicks", FormatInt(lootTable.MaxRandomPicks));

        changed |= SetYamlListBlocks(
            lines,
            "guaranteedEntries",
            lootTable.GuaranteedEntries
                .Select(entry => BuildLootEntryBlock(
                    lootTable,
                    entry,
                    itemGuidById,
                    lootTableGuidById,
                    warnings))
                .ToList());

        changed |= SetYamlListBlocks(
            lines,
            "weightedEntries",
            lootTable.WeightedEntries
                .Select(entry => BuildLootEntryBlock(
                    lootTable,
                    entry,
                    itemGuidById,
                    lootTableGuidById,
                    warnings))
                .ToList());

        if (!changed)
            return ExportAssetResult.Unchanged();

        CreateBackup(absoluteAssetPath);

        await File.WriteAllLinesAsync(
            absoluteAssetPath,
            lines,
            cancellationToken);

        return ExportAssetResult.Updated();
    }

    private async Task<string> WritePreviewJsonAsync(
        ContentDatabaseDto database,
        string unityProjectRootPath,
        CancellationToken cancellationToken)
    {
        string exportDirectory = Path.Combine(
            unityProjectRootPath,
            "ItemToolExports");

        Directory.CreateDirectory(exportDirectory);

        string outputPath = Path.Combine(
            exportDirectory,
            "itemtool_export_preview.json");

        database.ProjectSettings.UnityProjectRootPath = unityProjectRootPath;

        string json = JsonSerializer.Serialize(database, _jsonOptions);

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        return outputPath;
    }

    private static Dictionary<string, string> BuildUnityGuidByIdMap<T>(
        IEnumerable<T> assets,
        string unityProjectRootPath,
        Func<T, string> idSelector,
        Func<T, string> sourceAssetPathSelector,
        string assetLabel,
        List<string> warnings)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (T asset in assets)
        {
            string id = idSelector(asset);
            string sourceAssetPath = sourceAssetPathSelector(asset);

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (result.ContainsKey(id))
            {
                AddWarning(warnings, $"{assetLabel} \"{id}\" has a duplicated id. Only the first GUID will be used for loot references.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                AddWarning(warnings, $"{assetLabel} \"{id}\" has no SourceAssetPath. It cannot be referenced from exported loot tables.");
                continue;
            }

            string? absoluteAssetPath = ResolveUnityAssetPath(
                unityProjectRootPath,
                sourceAssetPath);

            if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            {
                AddWarning(warnings, $"{assetLabel} \"{id}\" source asset not found. It cannot be referenced from exported loot tables.");
                continue;
            }

            string? guid = TryReadGuidFromMeta(absoluteAssetPath + ".meta");

            if (string.IsNullOrWhiteSpace(guid))
            {
                AddWarning(warnings, $"{assetLabel} \"{id}\" has no readable .meta GUID. It cannot be referenced from exported loot tables.");
                continue;
            }

            result[id] = guid;
        }

        return result;
    }

    private static string? TryReadGuidFromMeta(string metaPath)
    {
        if (!File.Exists(metaPath))
            return null;

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

    private static bool SetScalar(
        List<string> lines,
        string fieldName,
        string value)
    {
        string prefix = $"{fieldName}:";

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string indentation = line[..(line.Length - trimmed.Length)];
            string newLine = $"{indentation}{fieldName}: {value}";

            if (string.Equals(line, newLine, StringComparison.Ordinal))
                return false;

            lines[i] = newLine;
            return true;
        }

        return false;
    }

    private static bool SetNestedScalar(
        List<string> lines,
        string parentFieldName,
        string childFieldName,
        string value)
    {
        string parentPrefix = $"{parentFieldName}:";
        string childPrefix = $"{childFieldName}:";

        bool insideParent = false;
        int parentIndent = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (!insideParent)
            {
                if (!trimmed.StartsWith(parentPrefix, StringComparison.Ordinal))
                    continue;

                insideParent = true;
                parentIndent = CountLeadingSpaces(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            int currentIndent = CountLeadingSpaces(line);

            if (currentIndent <= parentIndent)
                break;

            if (!trimmed.StartsWith(childPrefix, StringComparison.Ordinal))
                continue;

            string indentation = line[..(line.Length - trimmed.Length)];
            string newLine = $"{indentation}{childFieldName}: {value}";

            if (string.Equals(line, newLine, StringComparison.Ordinal))
                return false;

            lines[i] = newLine;
            return true;
        }

        return false;
    }

    private static bool SetYamlListBlocks(
        List<string> lines,
        string listFieldName,
        IReadOnlyList<IReadOnlyList<string>> blocks)
    {
        string listPrefix = $"{listFieldName}:";

        int listIndex = -1;
        int listIndent = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (!trimmed.StartsWith(listPrefix, StringComparison.Ordinal))
                continue;

            listIndex = i;
            listIndent = CountLeadingSpaces(lines[i]);
            break;
        }

        if (listIndex < 0)
            return false;

        string listIndentation = new(' ', listIndent);
        string newHeaderLine = blocks.Count == 0
            ? $"{listIndentation}{listFieldName}: []"
            : $"{listIndentation}{listFieldName}:";

        bool headerChanged = !string.Equals(lines[listIndex], newHeaderLine, StringComparison.Ordinal);
        lines[listIndex] = newHeaderLine;

        int removeStart = listIndex + 1;
        int removeEndExclusive = removeStart;

        while (removeEndExclusive < lines.Count)
        {
            string line = lines[removeEndExclusive];

            if (string.IsNullOrWhiteSpace(line))
            {
                removeEndExclusive++;
                continue;
            }

            int currentIndent = CountLeadingSpaces(line);
            string trimmed = line.TrimStart();

            if (currentIndent < listIndent)
                break;

            if (currentIndent == listIndent && !trimmed.StartsWith("- ", StringComparison.Ordinal))
                break;

            removeEndExclusive++;
        }

        List<string> newLines = new();

        string childIndentation = new(' ', listIndent + 2);

        foreach (IReadOnlyList<string> block in blocks)
        {
            if (block.Count == 0)
                continue;

            newLines.Add($"{listIndentation}- {block[0]}");

            for (int i = 1; i < block.Count; i++)
                newLines.Add($"{childIndentation}{block[i]}");
        }

        List<string> oldLines = lines
            .Skip(removeStart)
            .Take(removeEndExclusive - removeStart)
            .ToList();

        bool same = oldLines.SequenceEqual(newLines);

        if (same && !headerChanged)
            return false;

        lines.RemoveRange(
            removeStart,
            removeEndExclusive - removeStart);

        lines.InsertRange(
            removeStart,
            newLines);

        return true;
    }

    private static IReadOnlyList<string> BuildStatModifierBlock(
        StatModifierDto modifier,
        int index)
    {
        return new[]
        {
            $"statTypeAffected: {FormatEnum(modifier.StatType)}",
            "type: 100",
            $"value: {FormatFloat(modifier.Value)}",
            $"order: {FormatInt(index)}"
        };
    }

    private static IReadOnlyList<string> BuildBuffEffectBlock(
        BuffEffectDto buff,
        int index)
    {
        return new[]
        {
            $"applicationMode: {FormatEnum(buff.ApplicationMode)}",
            $"statType: {FormatEnum(buff.StatType)}",
            "modifierType: 100",
            $"baseValue: {FormatFloat(buff.Value)}",
            $"duration: {FormatFloat(buff.Duration)}"
        };
    }

    private static IReadOnlyList<string> BuildLootEntryBlock(
        LootTableDto owner,
        LootEntryDto entry,
        IReadOnlyDictionary<string, string> itemGuidById,
        IReadOnlyDictionary<string, string> lootTableGuidById,
        List<string> warnings)
    {
        string itemReference = "{fileID: 0}";
        string nestedTableReference = "{fileID: 0}";

        if (entry.EntryType == LootEntryType.Item)
        {
            string itemId = entry.ItemId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(itemId) &&
                itemGuidById.TryGetValue(itemId, out string itemGuid))
            {
                itemReference = ToUnityScriptableObjectReference(itemGuid);
            }
            else
            {
                AddWarning(warnings, $"Loot table \"{owner.Id}\" has an item entry with unresolved ItemId \"{itemId}\".");
            }
        }
        else if (entry.EntryType == LootEntryType.LootTable)
        {
            string nestedLootTableId = entry.NestedLootTableId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(nestedLootTableId) &&
                lootTableGuidById.TryGetValue(nestedLootTableId, out string lootTableGuid))
            {
                nestedTableReference = ToUnityScriptableObjectReference(lootTableGuid);
            }
            else
            {
                AddWarning(warnings, $"Loot table \"{owner.Id}\" has a nested table entry with unresolved NestedLootTableId \"{nestedLootTableId}\".");
            }
        }

        return new[]
        {
            $"entryType: {FormatEnum(entry.EntryType)}",
            $"item: {itemReference}",
            $"nestedTable: {nestedTableReference}",
            $"minQuantity: {FormatInt(entry.MinQuantity)}",
            $"maxQuantity: {FormatInt(entry.MaxQuantity)}",
            $"weight: {FormatInt(entry.Weight)}",
            $"equipableOverrideMode: {FormatEnum(entry.EquipableOverrideMode)}",
            $"overrideFixedRarity: {FormatEnum(entry.OverrideFixedRarity)}"
        };
    }

    private static string ToUnityScriptableObjectReference(string guid)
    {
        return $"{{fileID: 11400000, guid: {guid}, type: 2}}";
    }

    private static string? ResolveUnityAssetPath(
        string unityProjectRootPath,
        string sourceAssetPath)
    {
        if (string.IsNullOrWhiteSpace(sourceAssetPath))
            return null;

        string normalizedRoot = Path.GetFullPath(unityProjectRootPath);
        string normalizedSource = sourceAssetPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        string absolutePath = Path.IsPathRooted(normalizedSource)
            ? Path.GetFullPath(normalizedSource)
            : Path.GetFullPath(Path.Combine(normalizedRoot, normalizedSource));

        bool isInsideUnityProject = absolutePath.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase);

        return isInsideUnityProject
            ? absolutePath
            : null;
    }

    private static void CreateBackup(string assetPath)
    {
        string timestamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);

        string backupPath = $"{assetPath}.{timestamp}.bak";

        File.Copy(
            assetPath,
            backupPath,
            overwrite: false);
    }

    private static int GetUnityItemType(ItemDto item)
    {
        return item.ItemKind switch
        {
            ItemKind.Equipment => 0,
            ItemKind.Weapon => 0,
            ItemKind.Consumable => 1,
            ItemKind.Crafting => 2,
            ItemKind.Collectable => 3,
            _ => Convert.ToInt32(item.ItemType, CultureInfo.InvariantCulture)
        };
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool value)
    {
        return value ? "1" : "0";
    }

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return Convert
            .ToInt32(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);
    }

    private static int CountLeadingSpaces(string line)
    {
        int count = 0;

        foreach (char character in line)
        {
            if (character != ' ')
                break;

            count++;
        }

        return count;
    }

    private static void AddWarning(
        List<string> warnings,
        string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return;

        const int maxWarnings = 20;

        if (warnings.Count < maxWarnings)
        {
            warnings.Add(warning);
            return;
        }

        if (warnings.Count == maxWarnings)
            warnings.Add("Additional export warnings were omitted.");
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

    private enum ExportAssetStatus
    {
        Updated,
        Unchanged,
        Skipped
    }

    private sealed class ExportAssetResult
    {
        private ExportAssetResult(
            ExportAssetStatus status,
            string message = "")
        {
            Status = status;
            Message = message;
        }

        public ExportAssetStatus Status { get; }

        public string Message { get; }

        public static ExportAssetResult Updated()
        {
            return new ExportAssetResult(ExportAssetStatus.Updated);
        }

        public static ExportAssetResult Unchanged()
        {
            return new ExportAssetResult(ExportAssetStatus.Unchanged);
        }

        public static ExportAssetResult Skipped(string message)
        {
            return new ExportAssetResult(ExportAssetStatus.Skipped, message);
        }
    }
}