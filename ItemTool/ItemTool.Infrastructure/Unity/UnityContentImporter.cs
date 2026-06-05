using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;

namespace ItemTool.Infrastructure.Unity;

public sealed class UnityContentImporter : IUnityContentImporter
{
    public Task<UnityContentOperationResultDto> ImportAsync(
        string unityProjectRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidUnityProjectRoot(unityProjectRootPath, out string validationMessage))
            return Task.FromResult(UnityContentOperationResultDto.Failure(validationMessage));

        string assetsPath = Path.Combine(unityProjectRootPath, "Assets");

        Dictionary<string, string> guidToAssetPath = UnityAssetScanner.BuildGuidToAssetPathMap(
            unityProjectRootPath,
            assetsPath,
            cancellationToken);

        List<string> assetPaths = UnityAssetScanner.EnumerateAssetFiles(assetsPath);

        UnityReferenceIdMaps referenceIdMaps = UnityReferenceIdMaps.Build(
            unityProjectRootPath,
            assetPaths,
            guidToAssetPath,
            cancellationToken);

        List<ItemDto> importedItems = new();
        List<LootTableDto> importedLootTables = new();

        int scannedAssetCount = 0;
        int ignoredAssetCount = 0;

        foreach (string assetPath in assetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            scannedAssetCount++;

            string[]? lines = UnityAssetScanner.TryReadAllLines(assetPath);
            if (lines == null)
            {
                ignoredAssetCount++;
                continue;
            }

            string className = UnityClassResolver.ResolveUnityClassName(
                lines,
                guidToAssetPath);

            if (UnityClassResolver.IsSupportedItemClass(className))
            {
                ItemDto? item = UnityItemAssetImporter.TryImport(
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

            if (UnityClassResolver.IsLootTableClass(className))
            {
                LootTableDto? lootTable = UnityLootTableAssetImporter.TryImport(
                    unityProjectRootPath,
                    assetPath,
                    lines,
                    guidToAssetPath,
                    referenceIdMaps);

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

        return Task.FromResult(result);
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