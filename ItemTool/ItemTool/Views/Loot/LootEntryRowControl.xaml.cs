using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ItemTool.Application.DTOs;

namespace ItemTool.App.Views.Loot;

public partial class LootEntryRowControl : UserControl
{
    public static readonly DependencyProperty EntryProperty =
        DependencyProperty.Register(
            nameof(Entry),
            typeof(LootEntryDto),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowWeightProperty =
        DependencyProperty.Register(
            nameof(ShowWeight),
            typeof(bool),
            typeof(LootEntryRowControl),
            new PropertyMetadata(false, OnShowWeightChanged));

    public static readonly DependencyProperty WeightColumnWidthProperty =
        DependencyProperty.Register(
            nameof(WeightColumnWidth),
            typeof(GridLength),
            typeof(LootEntryRowControl),
            new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty EntryTypesProperty =
        DependencyProperty.Register(
            nameof(EntryTypes),
            typeof(IEnumerable),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableItemIdsProperty =
        DependencyProperty.Register(
            nameof(AvailableItemIds),
            typeof(IEnumerable),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableLootTableIdsProperty =
        DependencyProperty.Register(
            nameof(AvailableLootTableIds),
            typeof(IEnumerable),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EquipableOverrideModesProperty =
        DependencyProperty.Register(
            nameof(EquipableOverrideModes),
            typeof(IEnumerable),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemRaritiesProperty =
        DependencyProperty.Register(
            nameof(ItemRarities),
            typeof(IEnumerable),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RemoveCommandProperty =
        DependencyProperty.Register(
            nameof(RemoveCommand),
            typeof(ICommand),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty NavigateToItemCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateToItemCommand),
            typeof(ICommand),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty NavigateToLootTableCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateToLootTableCommand),
            typeof(ICommand),
            typeof(LootEntryRowControl),
            new PropertyMetadata(null));

    public LootEntryRowControl()
    {
        InitializeComponent();
        RefreshWeightColumn();
    }

    public LootEntryDto? Entry
    {
        get => (LootEntryDto?)GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    public bool ShowWeight
    {
        get => (bool)GetValue(ShowWeightProperty);
        set => SetValue(ShowWeightProperty, value);
    }

    public GridLength WeightColumnWidth
    {
        get => (GridLength)GetValue(WeightColumnWidthProperty);
        set => SetValue(WeightColumnWidthProperty, value);
    }

    public IEnumerable? EntryTypes
    {
        get => (IEnumerable?)GetValue(EntryTypesProperty);
        set => SetValue(EntryTypesProperty, value);
    }

    public IEnumerable? AvailableItemIds
    {
        get => (IEnumerable?)GetValue(AvailableItemIdsProperty);
        set => SetValue(AvailableItemIdsProperty, value);
    }

    public IEnumerable? AvailableLootTableIds
    {
        get => (IEnumerable?)GetValue(AvailableLootTableIdsProperty);
        set => SetValue(AvailableLootTableIdsProperty, value);
    }

    public IEnumerable? EquipableOverrideModes
    {
        get => (IEnumerable?)GetValue(EquipableOverrideModesProperty);
        set => SetValue(EquipableOverrideModesProperty, value);
    }

    public IEnumerable? ItemRarities
    {
        get => (IEnumerable?)GetValue(ItemRaritiesProperty);
        set => SetValue(ItemRaritiesProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ICommand? NavigateToItemCommand
    {
        get => (ICommand?)GetValue(NavigateToItemCommandProperty);
        set => SetValue(NavigateToItemCommandProperty, value);
    }

    public ICommand? NavigateToLootTableCommand
    {
        get => (ICommand?)GetValue(NavigateToLootTableCommandProperty);
        set => SetValue(NavigateToLootTableCommandProperty, value);
    }

    private static void OnShowWeightChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is LootEntryRowControl row)
            row.RefreshWeightColumn();
    }

    private void RefreshWeightColumn()
    {
        WeightColumnWidth = ShowWeight
            ? new GridLength(80)
            : new GridLength(0);
    }
}