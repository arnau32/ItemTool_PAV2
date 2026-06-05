namespace ItemTool.Infrastructure.Unity;

internal static class UnityAssetScanner
{
    public static List<string> EnumerateAssetFiles(string assetsPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(
                    assetsPath,
                    "*.asset",
                    SearchOption.AllDirectories)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static Dictionary<string, string> BuildGuidToAssetPathMap(
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

    public static string[]? TryReadAllLines(string path)
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

    public static string ToUnityAssetPath(
        string unityProjectRootPath,
        string absoluteAssetPath)
    {
        string relativePath = Path.GetRelativePath(
            unityProjectRootPath,
            absoluteAssetPath);

        return relativePath.Replace('\\', '/');
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
}