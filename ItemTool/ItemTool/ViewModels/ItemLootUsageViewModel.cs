namespace ItemTool.App.ViewModels;

public sealed class ItemLootUsageViewModel
{
    public ItemLootUsageViewModel(
        string lootTableId,
        string lootTableName,
        string entryGroup,
        int entryIndex)
    {
        LootTableId = lootTableId;
        LootTableName = lootTableName;
        EntryGroup = entryGroup;
        EntryIndex = entryIndex;
    }

    public string LootTableId { get; }

    public string LootTableName { get; }

    public string EntryGroup { get; }

    public int EntryIndex { get; }

    public string Title
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(LootTableName))
                return LootTableName;

            if (!string.IsNullOrWhiteSpace(LootTableId))
                return LootTableId;

            return "<Unnamed Loot Table>";
        }
    }

    public string Subtitle => $"{LootTableId} · {EntryGroup} entry #{EntryIndex}";
}