using System.Windows;
using System.Windows.Controls;

namespace ItemTool.App.Views.Loot;

public partial class LootEntryHeaderRow : UserControl
{
    public static readonly DependencyProperty ShowWeightProperty =
        DependencyProperty.Register(
            nameof(ShowWeight),
            typeof(bool),
            typeof(LootEntryHeaderRow),
            new PropertyMetadata(false, OnShowWeightChanged));

    public static readonly DependencyProperty WeightColumnWidthProperty =
        DependencyProperty.Register(
            nameof(WeightColumnWidth),
            typeof(GridLength),
            typeof(LootEntryHeaderRow),
            new PropertyMetadata(new GridLength(0)));

    public LootEntryHeaderRow()
    {
        InitializeComponent();
        RefreshWeightColumn();
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

    private static void OnShowWeightChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is LootEntryHeaderRow row)
            row.RefreshWeightColumn();
    }

    private void RefreshWeightColumn()
    {
        WeightColumnWidth = ShowWeight
            ? new GridLength(80)
            : new GridLength(0);
    }
}