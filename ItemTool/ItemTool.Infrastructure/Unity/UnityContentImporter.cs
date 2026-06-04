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

        int assetFileCount = CountAssetFiles(assetsPath);

        UnityContentOperationResultDto result = UnityContentOperationResultDto.Success(
            $"Import placeholder ready. Found {assetFileCount} .asset file(s). Real ScriptableObject parsing is not implemented yet.");

        result.Warnings.Add("This importer currently validates the Unity project and scans assets only.");
        result.Warnings.Add("Next step: map Unity ScriptableObject files to ItemDto and LootTableDto.");

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

    private static int CountAssetFiles(string assetsPath)
    {
        try
        {
            return Directory.EnumerateFiles(
                    assetsPath,
                    "*.asset",
                    SearchOption.AllDirectories)
                .Count();
        }
        catch
        {
            return 0;
        }
    }
}