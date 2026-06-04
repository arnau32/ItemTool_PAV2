using System.Collections.Generic;

#if UNITY_EDITOR
namespace ContentEditor.Modules.LootTables
{
    public enum LootTableSortMode
    {
        NameAz,
        NameZa,
        GuaranteedCount,
        WeightedCount
    }
    
    public enum LootTableValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public readonly struct LootTableValidationMessage
    {
        public readonly LootTableValidationSeverity Severity;
        public readonly string Message;

        public LootTableValidationMessage(LootTableValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public readonly struct LootTableValidationResult
    {
        public readonly LootTableValidationSeverity HighestSeverity;
        public readonly string Indicator;
        public readonly UnityEngine.Color Color;
        public readonly string Tooltip;
        public readonly bool HasIssues;

        public LootTableValidationResult(
            LootTableValidationSeverity highestSeverity,
            string indicator,
            UnityEngine.Color color,
            string tooltip,
            bool hasIssues)
        {
            HighestSeverity = highestSeverity;
            Indicator = indicator;
            Color = color;
            Tooltip = tooltip;
            HasIssues = hasIssues;
        }

        public static LootTableValidationResult None =>
            new(LootTableValidationSeverity.None, "", UnityEngine.Color.clear, "", false);
    }
    
    public sealed class LootTableSimulationRow
    {
        public ItemData Item;
        public int TotalQuantity;
        public int AppearsInRolls;

        public readonly Dictionary<Enums.ItemRarity, int> RarityCounts = new();
    }

    public sealed class LootTableSimulationSnapshot
    {
        public int Iterations;
        public int TotalGeneratedStacks;
        public int TotalGeneratedQuantity;

        public readonly List<LootTableSimulationRow> Rows = new();
        public LootTableInventoryPreviewSnapshot InventoryPreview;
    }
    
    public sealed class LootTableInventoryPlacedItem
    {
        public ItemData Item;
        public Enums.ItemRarity Rarity;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int Quantity;
    }

    public sealed class LootTableInventoryPreviewSnapshot
    {
        public readonly List<LootTableInventoryPlacedItem> Placed = new();
        public readonly List<ItemStack> Unplaced = new();
    }
    
}
#endif