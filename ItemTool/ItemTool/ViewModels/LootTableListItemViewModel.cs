using ItemTool.Application.DTOs;

namespace ItemTool.App.ViewModels;

public sealed class LootTableListItemViewModel : ViewModelBase
{
    public LootTableDto LootTable { get; }

    public LootTableListItemViewModel(LootTableDto lootTable)
    {
        LootTable = lootTable;

        LootTable.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LootTableDto.Name) ||
                e.PropertyName == nameof(LootTableDto.Id))
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Subtitle));
            }

            if (e.PropertyName == nameof(LootTableDto.MinRandomPicks) ||
                e.PropertyName == nameof(LootTableDto.MaxRandomPicks))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        };
    }

    public string Name
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(LootTable.Name))
                return LootTable.Name;

            if (!string.IsNullOrWhiteSpace(LootTable.Id))
                return LootTable.Id;

            return "<Unnamed Loot Table>";
        }
    }

    public string Subtitle =>
        $"{LootTable.Id} · Picks {LootTable.MinRandomPicks}-{LootTable.MaxRandomPicks}";
}