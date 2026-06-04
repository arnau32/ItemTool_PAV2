using System.Text.Json;
using System.Text.Json.Serialization;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;

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

        UnityContentOperationResultDto result = UnityContentOperationResultDto.Success(
            $"Export preview completed. Wrote {database.Items.Count} item(s) and {database.LootTables.Count} loot table(s).",
            outputPath);

        result.Warnings.Add("This is a preview JSON export, not real Unity ScriptableObject generation yet.");
        result.Warnings.Add("Next step: write/update Unity assets from DTOs.");

        return result;
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