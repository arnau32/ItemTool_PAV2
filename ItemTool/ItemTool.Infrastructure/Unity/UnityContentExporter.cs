using System.Globalization;
using System.Text;
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

        List<string> warnings = new();

        int createdItemAssets = await EnsureNewItemAssetsAsync(
            database.Items,
            unityProjectRootPath,
            warnings,
            cancellationToken);

        int createdLootTableAssets = await EnsureNewLootTableAssetsAsync(
            database.LootTables,
            unityProjectRootPath,
            warnings,
            cancellationToken);

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

        int updatedItems = 0;
        int updatedLootTables = 0;
        int skippedItems = 0;
        int skippedLootTables = 0;
        int unchangedAssets = 0;

        foreach (ItemDto item in database.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExportAssetResult result = await ExportItemAsync(
                item,
                unityProjectRootPath,
                warnings,
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
            $"Unity export completed. Created {createdItemAssets} item asset(s) and {createdLootTableAssets} loot table asset(s). Updated {updatedItems} item asset(s) and {updatedLootTables} loot table asset(s). Skipped {skippedItems} item(s), {skippedLootTables} loot table(s), {unchangedAssets} unchanged asset(s).",
            previewPath);

        foreach (string warning in warnings)
            operationResult.Warnings.Add(warning);

        operationResult.Warnings.Add("[MINOR] Export creates new Unity .asset files for local assets without SourceAssetPath.");
        operationResult.Warnings.Add("[MINOR] Icon refs are exported when IconPath points to a valid Unity asset.");
        operationResult.Warnings.Add("[MINOR] Prefab refs are intentionally not exported because Unity prefab fileIDs can be object-specific.");
        operationResult.Warnings.Add("[MINOR] WPF does not delete Unity assets. Removed local items may leave Unity assets orphaned.");
        operationResult.Warnings.Add("[MINOR] A timestamped .bak file is created before each modified existing .asset is overwritten.");

        return operationResult;
    }

    private static async Task<int> EnsureNewItemAssetsAsync(
        IEnumerable<ItemDto> items,
        string unityProjectRootPath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        int createdCount = 0;

        foreach (ItemDto item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(item.SourceAssetPath))
                continue;

            string className = GetUnityItemClassName(item);
            string? scriptGuid = FindScriptGuid(unityProjectRootPath, className);

            if (string.IsNullOrWhiteSpace(scriptGuid))
            {
                AddWarning(warnings, $"[MAJOR] Cannot create Unity asset for item \"{item.Id}\" because script \"{className}.cs.meta\" was not found.");
                continue;
            }

            string folderUnityPath = GetDefaultUnityItemFolder(item);
            string folderAbsolutePath = Path.Combine(
                unityProjectRootPath,
                folderUnityPath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(folderAbsolutePath);

            string assetName = CreateAssetName(item.Id, item.ItemNameId, item.DisplayName, item.ItemKind.ToString());
            string absoluteAssetPath = CreateUniqueAssetPath(folderAbsolutePath, assetName);

            item.SourceAssetPath = ToUnityAssetPath(
                unityProjectRootPath,
                absoluteAssetPath);

            List<string> assetLines = BuildNewItemAssetLines(
                item,
                Path.GetFileNameWithoutExtension(absoluteAssetPath),
                className,
                scriptGuid);

            await File.WriteAllLinesAsync(
                absoluteAssetPath,
                assetLines,
                cancellationToken);

            await File.WriteAllLinesAsync(
                absoluteAssetPath + ".meta",
                BuildNewAssetMetaLines(),
                cancellationToken);

            createdCount++;
        }

        return createdCount;
    }

    private static async Task<int> EnsureNewLootTableAssetsAsync(
        IEnumerable<LootTableDto> lootTables,
        string unityProjectRootPath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        int createdCount = 0;

        foreach (LootTableDto lootTable in lootTables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(lootTable.SourceAssetPath))
                continue;

            const string className = "LootTable";
            string? scriptGuid = FindScriptGuid(unityProjectRootPath, className);

            if (string.IsNullOrWhiteSpace(scriptGuid))
            {
                AddWarning(warnings, $"[MAJOR] Cannot create Unity asset for loot table \"{lootTable.Id}\" because script \"{className}.cs.meta\" was not found.");
                continue;
            }

            const string folderUnityPath = "Assets/Scriptable Objects/Items/LootTables";
            string folderAbsolutePath = Path.Combine(
                unityProjectRootPath,
                folderUnityPath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(folderAbsolutePath);

            string assetName = CreateAssetName(lootTable.Id, lootTable.Name, "LootTable", "LootTable");
            string absoluteAssetPath = CreateUniqueAssetPath(folderAbsolutePath, assetName);

            lootTable.SourceAssetPath = ToUnityAssetPath(
                unityProjectRootPath,
                absoluteAssetPath);

            List<string> assetLines = BuildNewLootTableAssetLines(
                lootTable,
                Path.GetFileNameWithoutExtension(absoluteAssetPath),
                scriptGuid);

            await File.WriteAllLinesAsync(
                absoluteAssetPath,
                assetLines,
                cancellationToken);

            await File.WriteAllLinesAsync(
                absoluteAssetPath + ".meta",
                BuildNewAssetMetaLines(),
                cancellationToken);

            createdCount++;
        }

        return createdCount;
    }

    private static async Task<ExportAssetResult> ExportItemAsync(
        ItemDto item,
        string unityProjectRootPath,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.SourceAssetPath))
            return ExportAssetResult.Skipped($"[MAJOR] Item \"{item.Id}\" has no SourceAssetPath.");

        string? absoluteAssetPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            item.SourceAssetPath);

        if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            return ExportAssetResult.Skipped($"[MAJOR] Item \"{item.Id}\" source asset not found: {item.SourceAssetPath}");

        List<string> lines = (await File.ReadAllLinesAsync(
                absoluteAssetPath,
                cancellationToken))
            .ToList();

        bool changed = false;

        changed |= SetScalar(lines, "<uniqueID>k__BackingField", SafeYamlValue(item.Id));
        changed |= SetScalar(lines, "itemType", FormatInt(GetUnityItemType(item)));
        changed |= SetScalar(lines, "itemNameID", SafeYamlValue(item.ItemNameId));
        changed |= SetScalar(lines, "description", SafeYamlValue(item.Description));

        string? iconReference = TryBuildIconReference(
            item,
            unityProjectRootPath,
            warnings);

        if (iconReference != null)
            changed |= SetScalar(lines, "icon", iconReference);

        changed |= SetScalar(lines, "itemRarity", FormatEnum(item.ItemRarity));
        changed |= SetScalar(lines, "value", FormatFloat(item.Value));
        changed |= SetScalar(lines, "weight", FormatFloat(item.Weight));
        changed |= SetScalar(lines, "maxStack", FormatInt(item.MaxStack));
        changed |= SetNestedScalar(lines, "SlotDimension", "Height", FormatInt(item.SlotDimension.Height));
        changed |= SetNestedScalar(lines, "SlotDimension", "Width", FormatInt(item.SlotDimension.Width));

        if (item.Equipable != null)
        {
            ValidatePrefabPath(
                item,
                unityProjectRootPath,
                warnings);

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
            return ExportAssetResult.Skipped($"[MAJOR] Loot table \"{lootTable.Id}\" has no SourceAssetPath.");

        string? absoluteAssetPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            lootTable.SourceAssetPath);

        if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            return ExportAssetResult.Skipped($"[MAJOR] Loot table \"{lootTable.Id}\" source asset not found: {lootTable.SourceAssetPath}");

        List<string> lines = (await File.ReadAllLinesAsync(
                absoluteAssetPath,
                cancellationToken))
            .ToList();

        bool changed = false;

        changed |= SetScalar(lines, "lootTableID", SafeYamlValue(lootTable.Id));
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
                AddWarning(warnings, $"[MAJOR] {assetLabel} \"{id}\" has a duplicated id. Only the first GUID will be used for loot references.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                AddWarning(warnings, $"[MAJOR] {assetLabel} \"{id}\" has no SourceAssetPath. It cannot be referenced from exported loot tables.");
                continue;
            }

            string? absoluteAssetPath = ResolveUnityAssetPath(
                unityProjectRootPath,
                sourceAssetPath);

            if (string.IsNullOrWhiteSpace(absoluteAssetPath) || !File.Exists(absoluteAssetPath))
            {
                AddWarning(warnings, $"[MAJOR] {assetLabel} \"{id}\" source asset not found. It cannot be referenced from exported loot tables.");
                continue;
            }

            string? guid = TryReadGuidFromMeta(absoluteAssetPath + ".meta");

            if (string.IsNullOrWhiteSpace(guid))
            {
                AddWarning(warnings, $"[MAJOR] {assetLabel} \"{id}\" has no readable .meta GUID. It cannot be referenced from exported loot tables.");
                continue;
            }

            result[id] = guid;
        }

        return result;
    }

    private static List<string> BuildNewItemAssetLines(
        ItemDto item,
        string assetName,
        string className,
        string scriptGuid)
    {
        item.EnsureDetailsForCurrentKind();

        List<string> lines = BuildUnityAssetHeader(
            assetName,
            className,
            scriptGuid);

        lines.Add($"  <uniqueID>k__BackingField: {SafeYamlValue(item.Id)}");
        lines.Add("  icon: {fileID: 0}");
        lines.Add($"  itemType: {FormatInt(GetUnityItemType(item))}");
        lines.Add($"  itemNameID: {SafeYamlValue(item.ItemNameId)}");
        lines.Add("  localizedDisplayName:");
        lines.Add("    m_TableReference:");
        lines.Add("      m_TableCollectionName: ");
        lines.Add("    m_TableEntryReference:");
        lines.Add("      m_KeyId: 0");
        lines.Add("      m_Key: ");
        lines.Add("    m_FallbackState: 0");
        lines.Add("    m_WaitForCompletion: 0");
        lines.Add("    m_LocalVariables: []");
        lines.Add($"  description: {SafeYamlValue(item.Description)}");
        lines.Add("  descriptionBlocks: []");
        lines.Add($"  itemRarity: {FormatEnum(item.ItemRarity)}");
        lines.Add($"  value: {FormatFloat(item.Value)}");
        lines.Add($"  weight: {FormatFloat(item.Weight)}");
        lines.Add($"  maxStack: {FormatInt(item.MaxStack)}");
        lines.Add("  SlotDimension:");
        lines.Add($"    Height: {FormatInt(item.SlotDimension.Height)}");
        lines.Add($"    Width: {FormatInt(item.SlotDimension.Width)}");

        if (item.Equipable != null)
        {
            lines.Add($"  equipSlot: {FormatEnum(item.Equipable.EquipSlot)}");
            lines.Add("  modifiers: []");
            lines.Add("  prefab: {fileID: 0}");
            lines.Add($"  tier: {FormatInt(item.Equipable.Tier)}");
            lines.Add($"  rollMode: {FormatEnum(item.Equipable.RollMode)}");
        }

        if (item.Weapon != null)
        {
            lines.Add("  animatorOverride: {fileID: 0}");
            lines.Add($"  handType: {FormatEnum(item.Weapon.HandType)}");
            lines.Add($"  familyType: {FormatEnum(item.Weapon.FamilyType)}");
            lines.Add("  prefabVariant: {fileID: 0}");
            lines.Add("  combos: []");
            lines.Add("  dodgeSet: {fileID: 0}");
            lines.Add("  parry: {fileID: 0}");
            lines.Add("  weaponSkill: {fileID: 0}");
            lines.Add($"  skillScoreNeeded: {FormatFloat(item.Weapon.SkillScoreNeeded)}");
            lines.Add($"  weaponTier: {FormatInt(item.Weapon.WeaponTier)}");
            lines.Add($"  enemyDamageMultiplier: {FormatFloat(item.Weapon.EnemyDamageMultiplier)}");
        }

        if (item.Consumable != null)
            lines.Add("  buffs: []");

        if (item.Collectable != null)
        {
            lines.Add($"  collectionID: {FormatInt(item.Collectable.CollectionId)}");
            lines.Add($"  isAuroraDust: {FormatBool(item.Collectable.IsAuroraDust)}");
        }

        lines.Add("  references:");
        lines.Add("    version: 2");
        lines.Add("    RefIds: []");

        return lines;
    }

    private static List<string> BuildNewLootTableAssetLines(
        LootTableDto lootTable,
        string assetName,
        string scriptGuid)
    {
        List<string> lines = BuildUnityAssetHeader(
            assetName,
            "LootTable",
            scriptGuid);

        lines.Add($"  lootTableID: {SafeYamlValue(lootTable.Id)}");
        lines.Add("  guaranteedEntries: []");
        lines.Add("  weightedEntries: []");
        lines.Add($"  minRandomPicks: {FormatInt(lootTable.MinRandomPicks)}");
        lines.Add($"  maxRandomPicks: {FormatInt(lootTable.MaxRandomPicks)}");

        return lines;
    }

    private static List<string> BuildUnityAssetHeader(
        string assetName,
        string className,
        string scriptGuid)
    {
        return new List<string>
        {
            "%YAML 1.1",
            "%TAG !u! tag:unity3d.com,2011:",
            "--- !u!114 &11400000",
            "MonoBehaviour:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            "  m_GameObject: {fileID: 0}",
            "  m_Enabled: 1",
            "  m_EditorHideFlags: 0",
            $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}",
            $"  m_Name: {SafeYamlValue(assetName)}",
            $"  m_EditorClassIdentifier: Assembly-CSharp::{className}"
        };
    }

    private static IReadOnlyList<string> BuildNewAssetMetaLines()
    {
        return new[]
        {
            "fileFormatVersion: 2",
            $"guid: {Guid.NewGuid():N}",
            "NativeFormatImporter:",
            "  externalObjects: {}",
            "  mainObjectFileID: 11400000",
            "  userData: ",
            "  assetBundleName: ",
            "  assetBundleVariant: "
        };
    }

    private static string? TryBuildIconReference(
        ItemDto item,
        string unityProjectRootPath,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(item.IconPath))
            return "{fileID: 0}";

        string? absoluteIconPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            item.IconPath);

        if (string.IsNullOrWhiteSpace(absoluteIconPath) || !File.Exists(absoluteIconPath))
        {
            AddWarning(
                warnings,
                $"[MINOR] Item \"{item.Id}\" has an IconPath that does not exist in the Unity project: {item.IconPath}");

            return null;
        }

        string? guid = TryReadGuidFromMeta(absoluteIconPath + ".meta");

        if (string.IsNullOrWhiteSpace(guid))
        {
            AddWarning(
                warnings,
                $"[MINOR] Item \"{item.Id}\" icon asset has no readable .meta GUID: {item.IconPath}");

            return null;
        }

        return $"{{fileID: 21300000, guid: {guid}, type: 3}}";
    }

    private static void ValidatePrefabPath(
        ItemDto item,
        string unityProjectRootPath,
        List<string> warnings)
    {
        string? prefabPath = item.Equipable?.PrefabPath;

        if (string.IsNullOrWhiteSpace(prefabPath))
            return;

        string? absolutePrefabPath = ResolveUnityAssetPath(
            unityProjectRootPath,
            prefabPath);

        if (string.IsNullOrWhiteSpace(absolutePrefabPath) || !File.Exists(absolutePrefabPath))
        {
            AddWarning(
                warnings,
                $"[MINOR] Item \"{item.Id}\" has a PrefabPath that does not exist in the Unity project: {prefabPath}");

            return;
        }

        string? guid = TryReadGuidFromMeta(absolutePrefabPath + ".meta");

        if (string.IsNullOrWhiteSpace(guid))
        {
            AddWarning(
                warnings,
                $"[MINOR] Item \"{item.Id}\" prefab asset has no readable .meta GUID: {prefabPath}");
        }
    }

    private static string? FindScriptGuid(
        string unityProjectRootPath,
        string className)
    {
        string assetsPath = Path.Combine(unityProjectRootPath, "Assets");

        if (!Directory.Exists(assetsPath))
            return null;

        try
        {
            string expectedMetaFileName = $"{className}.cs.meta";

            foreach (string metaPath in Directory.EnumerateFiles(
                         assetsPath,
                         expectedMetaFileName,
                         SearchOption.AllDirectories))
            {
                string? guid = TryReadGuidFromMeta(metaPath);

                if (!string.IsNullOrWhiteSpace(guid))
                    return guid;
            }
        }
        catch
        {
            return null;
        }

        return null;
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
                AddWarning(warnings, $"[MAJOR] Loot table \"{owner.Id}\" has an item entry with unresolved ItemId \"{itemId}\".");
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
                AddWarning(warnings, $"[MAJOR] Loot table \"{owner.Id}\" has a nested table entry with unresolved NestedLootTableId \"{nestedLootTableId}\".");
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

        string relativePath = Path.GetRelativePath(
            normalizedRoot,
            absolutePath);

        bool isInsideUnityProject =
            relativePath == "." ||
            (!relativePath.StartsWith("..", StringComparison.Ordinal) &&
             !Path.IsPathRooted(relativePath));

        return isInsideUnityProject
            ? absolutePath
            : null;
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

    private static string CreateAssetName(
        params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            string sanitized = SanitizeFileName(candidate);

            if (!string.IsNullOrWhiteSpace(sanitized))
                return sanitized;
        }

        return "NewAsset";
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();

        string sanitized = new(
            value
                .Select(character =>
                    invalidCharacters.Contains(character) ||
                    char.IsWhiteSpace(character)
                        ? '_'
                        : character)
                .ToArray());

        return sanitized.Trim('_');
    }

    private static string CreateUniqueAssetPath(
        string folderAbsolutePath,
        string assetName)
    {
        string basePath = Path.Combine(folderAbsolutePath, $"{assetName}.asset");

        if (!File.Exists(basePath))
            return basePath;

        int index = 1;

        while (true)
        {
            string candidate = Path.Combine(folderAbsolutePath, $"{assetName}_{index}.asset");

            if (!File.Exists(candidate))
                return candidate;

            index++;
        }
    }

    private static string GetDefaultUnityItemFolder(ItemDto item)
    {
        return item.ItemKind switch
        {
            ItemKind.Equipment => "Assets/Scriptable Objects/Items/Equipment",
            ItemKind.Weapon => "Assets/Scriptable Objects/Items/Weapons",
            ItemKind.Consumable => "Assets/Scriptable Objects/Items/Consumables",
            ItemKind.Crafting => "Assets/Scriptable Objects/Items/Crafting",
            ItemKind.Collectable => "Assets/Scriptable Objects/Items/Collectables",
            _ => "Assets/Scriptable Objects/Items"
        };
    }

    private static string GetUnityItemClassName(ItemDto item)
    {
        return item.ItemKind switch
        {
            ItemKind.Equipment => "EquipableItemData",
            ItemKind.Weapon => "WeaponData",
            ItemKind.Consumable => "ConsumableItemData",
            ItemKind.Crafting => "CraftingItemData",
            ItemKind.Collectable => "CollectableItemData",
            _ => "ItemData"
        };
    }

    private static void CreateBackup(string assetPath)
    {
        string timestamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss_fff",
            CultureInfo.InvariantCulture);

        string backupPath = $"{assetPath}.{timestamp}.bak";

        if (!File.Exists(backupPath))
        {
            File.Copy(
                assetPath,
                backupPath,
                overwrite: false);

            return;
        }

        int index = 1;

        while (true)
        {
            string candidate = $"{assetPath}.{timestamp}_{index}.bak";

            if (!File.Exists(candidate))
            {
                File.Copy(
                    assetPath,
                    candidate,
                    overwrite: false);

                return;
            }

            index++;
        }
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

    private static string SafeYamlValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool needsQuotes =
            value.Any(char.IsControl) ||
            value.StartsWith(' ') ||
            value.EndsWith(' ') ||
            value.Contains(':') ||
            value.Contains('#') ||
            value.Contains('{') ||
            value.Contains('}') ||
            value.Contains('[') ||
            value.Contains(']') ||
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\'') ||
            value.Contains('\n') ||
            value.Contains('\r') ||
            string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

        if (!needsQuotes)
            return value;

        StringBuilder builder = new();
        builder.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;

                case '"':
                    builder.Append("\\\"");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');

        return builder.ToString();
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

        if (warnings.Contains(warning))
            return;

        const int maxWarnings = 30;

        if (warnings.Count < maxWarnings)
        {
            warnings.Add(warning);
            return;
        }

        const string omittedWarning = "[MINOR] Additional export warnings were omitted.";

        if (!warnings.Contains(omittedWarning))
            warnings.Add(omittedWarning);
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
            validationMessage = "Unity project folder does not exist.";
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