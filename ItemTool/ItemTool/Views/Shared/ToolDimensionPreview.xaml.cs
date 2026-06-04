using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ItemTool.App.Views.Shared;

public partial class ToolDimensionPreview : UserControl
{
    private const int MaxPreviewSize = 10;

    public static readonly DependencyProperty CellColumnsProperty =
        DependencyProperty.Register(
            nameof(CellColumns),
            typeof(int),
            typeof(ToolDimensionPreview),
            new PropertyMetadata(0, OnPreviewPropertyChanged));

    public static readonly DependencyProperty CellRowsProperty =
        DependencyProperty.Register(
            nameof(CellRows),
            typeof(int),
            typeof(ToolDimensionPreview),
            new PropertyMetadata(0, OnPreviewPropertyChanged));

    public static readonly DependencyProperty CellSizeProperty =
        DependencyProperty.Register(
            nameof(CellSize),
            typeof(double),
            typeof(ToolDimensionPreview),
            new PropertyMetadata(28d, OnPreviewPropertyChanged));

    public ToolDimensionPreview()
    {
        InitializeComponent();
        RebuildPreview();
    }

    public int CellColumns
    {
        get => (int)GetValue(CellColumnsProperty);
        set => SetValue(CellColumnsProperty, value);
    }

    public int CellRows
    {
        get => (int)GetValue(CellRowsProperty);
        set => SetValue(CellRowsProperty, value);
    }

    public double CellSize
    {
        get => (double)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    private static void OnPreviewPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ToolDimensionPreview preview)
            preview.RebuildPreview();
    }

    private void RebuildPreview()
    {
        PreviewGrid.Children.Clear();
        PreviewGrid.ColumnDefinitions.Clear();
        PreviewGrid.RowDefinitions.Clear();

        if (CellColumns < 1 || CellRows < 1)
        {
            SizeLabel.Text = "No dimensions selected";
            return;
        }

        int columns = Math.Min(CellColumns, MaxPreviewSize);
        int rows = Math.Min(CellRows, MaxPreviewSize);

        SizeLabel.Text = CellColumns == columns && CellRows == rows
            ? $"{CellColumns} x {CellRows}"
            : $"{CellColumns} x {CellRows}  (preview capped)";

        for (int x = 0; x < columns; x++)
        {
            PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(CellSize)
            });
        }

        for (int y = 0; y < rows; y++)
        {
            PreviewGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(CellSize)
            });
        }

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Border cell = new()
                {
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(58, 58, 58)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
                    BorderThickness = new Thickness(1)
                };

                Grid.SetColumn(cell, x);
                Grid.SetRow(cell, y);

                PreviewGrid.Children.Add(cell);
            }
        }
    }
}