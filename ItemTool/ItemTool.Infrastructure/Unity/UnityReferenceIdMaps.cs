namespace ItemTool.Infrastructure.Unity;

internal sealed class UnityReferenceIdMaps
{
    private UnityReferenceIdMaps(
        Dictionary<string, string> itemIdByUnityPath,
        Dictionary<string, string> lootTableIdByUnityPath)
    {
        ItemIdByUnityPath = itemIdByUnityPath;
        LootTableIdByUnityPath = lootTableIdByUnityPath;
    }

    public IReadOnlyDictionary<string, string> ItemIdByUnityPath { get; }

    public IReadOnlyDictionary<string, string> LootTableIdByUnityPath { get; }

    public static UnityReferenceIdMaps Build(
        string unityProjectRootPath,
        IEnumerable<string> assetPaths,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> itemIdByUnityPath = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> lootTableIdByUnityPath = new(StringComparer.OrdinalIgnoreCase);

        foreach (string assetPath in assetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[]? lines = UnityAssetScanner.TryReadAllLines(assetPath);
            if (lines == null)
                continue;

            string className = UnityClassResolver.ResolveUnityClassName(lines, guidToAssetPath);
            string unityPath = UnityAssetScanner.ToUnityAssetPath(unityProjectRootPath, assetPath);
            string assetName = UnityYamlReader.ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);

            if (UnityClassResolver.IsSupportedItemClass(className))
            {
                string itemId = ResolveItemId(lines, assetName);
                itemIdByUnityPath[unityPath] = itemId;
                continue;
            }

            if (UnityClassResolver.IsLootTableClass(className))
            {
                string lootTableId = ResolveLootTableId(lines, assetName);
                lootTableIdByUnityPath[unityPath] = lootTableId;
            }
        }

        return new UnityReferenceIdMaps(
            itemIdByUnityPath,
            lootTableIdByUnityPath);
    }

    private static string ResolveItemId(
        IReadOnlyList<string> lines,
        string assetName)
    {
        string uniqueId = UnityYamlReader.ReadScalar(lines, "<uniqueID>k__BackingField") ?? string.Empty;
        string itemNameId = UnityYamlReader.ReadScalar(lines, "itemNameID") ?? uniqueId;

        if (string.IsNullOrWhiteSpace(itemNameId))
            itemNameId = assetName;

        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = itemNameId;

        return uniqueId;
    }

    private static string ResolveLootTableId(
        IReadOnlyList<string> lines,
        string assetName)
    {
        string lootTableId = UnityYamlReader.ReadScalar(lines, "lootTableID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lootTableId))
            lootTableId = assetName;

        return lootTableId;
    }
}