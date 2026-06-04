using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ItemTool.App.Visuals;

public static class ItemIconSourceLoader
{
    private static readonly string[] SupportedExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif"
    };

    public static ImageSource? Load(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        string? resolvedPath = ResolveIconPath(iconPath);

        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            return null;

        try
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(resolvedPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveIconPath(string iconPath)
    {
        string normalizedPath = iconPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim();

        if (Path.IsPathRooted(normalizedPath))
            return ResolveExistingFile(normalizedPath);

        foreach (string basePath in GetSearchBasePaths())
        {
            string? directCandidate = ResolveExistingFile(Path.Combine(basePath, normalizedPath));
            if (directCandidate != null)
                return directCandidate;

            string? childCandidate = ResolveInOneLevelChildren(basePath, normalizedPath);
            if (childCandidate != null)
                return childCandidate;
        }

        return null;
    }

    private static string? ResolveExistingFile(string candidate)
    {
        if (File.Exists(candidate) && IsSupportedImage(candidate))
            return candidate;

        string extension = Path.GetExtension(candidate);

        if (!string.IsNullOrWhiteSpace(extension))
            return null;

        foreach (string supportedExtension in SupportedExtensions)
        {
            string candidateWithExtension = candidate + supportedExtension;

            if (File.Exists(candidateWithExtension))
                return candidateWithExtension;
        }

        return null;
    }

    private static string? ResolveInOneLevelChildren(string basePath, string relativePath)
    {
        if (!Directory.Exists(basePath))
            return null;

        try
        {
            foreach (string childDirectory in Directory.EnumerateDirectories(basePath))
            {
                string? candidate = ResolveExistingFile(Path.Combine(childDirectory, relativePath));

                if (candidate != null)
                    return candidate;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> GetSearchBasePaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        AddPathWithParents(paths, AppContext.BaseDirectory);
        AddPathWithParents(paths, Directory.GetCurrentDirectory());

        return paths;
    }

    private static void AddPathWithParents(HashSet<string> paths, string startPath)
    {
        DirectoryInfo? directory = new(startPath);

        for (int i = 0; i < 10 && directory != null; i++)
        {
            paths.Add(directory.FullName);
            directory = directory.Parent;
        }
    }

    private static bool IsSupportedImage(string path)
    {
        string extension = Path.GetExtension(path);

        return SupportedExtensions.Any(x =>
            string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
    }
}