#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class LootTableBrowserWindow
{
    internal enum SortMode
    {
        NameAz,
        NameZa,
        GuaranteedCount,
        WeightedCount
    }

    [Flags]
    private enum RefreshFlags
    {
        None = 0,
        Assets = 1 << 0,
        Filter = 1 << 1,
        Validations = 1 << 2,
        Details = 1 << 3,
        List = 1 << 4,
        Simulation = 1 << 5,

        Soft = Filter | Validations | List | Details | Simulation,
        Hard = Assets | Filter | Validations | List | Details | Simulation
    }

    internal enum ValidationSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    internal enum EntrySortDirection
    {
        Asc,
        Desc
    }

    internal sealed class ValidationMessage
    {
        public ValidationSeverity Severity;
        public string Message;
    }

    internal sealed class ListRowRefs
    {
        public Label Name;
        public Label Summary;
        public Label Indicator;
    }

    internal sealed class SimRow
    {
        public ItemData Item;
        public int TotalQuantity;
        public int AppearsInRolls;

        // Aggregated rarity distribution for this item across the simulation.
        // The value stores generated quantity, not roll count, so stackable items keep correct proportions.
        public readonly Dictionary<Enums.ItemRarity, int> RarityCounts = new();
    }

    internal sealed class InventoryPlacedItem
    {
        public ItemData Item;
        public Enums.ItemRarity Rarity;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int Quantity;
    }

    internal sealed class InventoryPreviewSnapshot
    {
        public readonly List<InventoryPlacedItem> Placed = new();
        public readonly List<ItemStack> Unplaced = new();
    }

    internal sealed class SimulationSnapshot
    {
        public int Iterations;
        public int TotalGeneratedStacks;
        public int TotalGeneratedQuantity;
        public readonly List<SimRow> Rows = new();

        public InventoryPreviewSnapshot InventoryPreview;
    }
}
#endif