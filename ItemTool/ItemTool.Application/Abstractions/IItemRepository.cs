using ItemTool.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace ItemTool.Application.Abstractions
{
    public interface IItemRepository
    {
        Task<IReadOnlyList<ItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task SaveAllAsync(IReadOnlyList<ItemDto> items, CancellationToken cancellationToken = default);
    }
}
