using System.IO;

namespace ItemTool.App.Services;

public static class UnityAssetPathUtility
{
    private const string AssetsFolderName = "Assets";

    public static string ToUnityAssetPathIfPossible(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string normalizedPath = Path.GetFullPath(path)
            .Replace('\\', '/')
            .Trim();

        string[] parts = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        int assetsIndex = FindLastAssetsFolderIndex(parts);

        if (assetsIndex < 0)
            return path;

        string unityPath = string.Join('/', parts.Skip(assetsIndex));

        return string.IsNullOrWhiteSpace(unityPath)
            ? path
            : unityPath;
    }

    public static bool IsUnityAssetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalizedPath = path.Replace('\\', '/').Trim();

        return normalizedPath.Equals(AssetsFolderName, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith($"{AssetsFolderName}/", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindLastAssetsFolderIndex(IReadOnlyList<string> parts)
    {
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            if (string.Equals(parts[i], AssetsFolderName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}