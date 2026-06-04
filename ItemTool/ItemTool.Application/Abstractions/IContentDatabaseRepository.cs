using ItemTool.Application.DTOs;

namespace ItemTool.Application.Abstractions;

public interface IContentDatabaseRepository
{
    Task<ContentDatabaseDto> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ContentDatabaseDto database, CancellationToken cancellationToken = default);
}