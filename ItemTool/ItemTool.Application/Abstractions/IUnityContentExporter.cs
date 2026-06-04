using ItemTool.Application.DTOs;

namespace ItemTool.Application.Abstractions;

public interface IUnityContentExporter
{
    Task<UnityContentOperationResultDto> ExportAsync(
        ContentDatabaseDto database,
        string unityProjectRootPath,
        CancellationToken cancellationToken = default);
}