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

        operationResult.Warnings.Add("Export phase 1 only writes safe scalar fields.");
        operationResult.Warnings.Add("References, modifiers, buffs and loot entries are not exported yet.");
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
        }

        if (item.Weapon != null)
        {
            changed |= SetScalar(lines, "handType", FormatEnum(item.Weapon.HandType));
            changed |= SetScalar(lines, "familyType", FormatEnum(item.Weapon.FamilyType));
            changed |= SetScalar(lines, "skillScoreNeeded", FormatFloat(item.Weapon.SkillScoreNeeded));
            changed |= SetScalar(lines, "weaponTier", FormatInt(item.Weapon.WeaponTier));
            changed |= SetScalar(lines, "enemyDamageMultiplier", FormatFloat(item.Weapon.EnemyDamageMultiplier));
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