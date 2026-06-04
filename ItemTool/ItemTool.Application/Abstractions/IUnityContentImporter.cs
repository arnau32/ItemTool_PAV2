using ItemTool.Application.DTOs;

namespace ItemTool.Application.Abstractions;

public interface IUnityContentImporter
{
    Task<UnityContentOperationResultDto> ImportAsync(
        string unityProjectRootPath,
        CancellationToken cancellationToken = default);
}