using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ItemTool.App.Views.Shared;

public partial class ToolAssetHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata("No item selected"));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TechnicalInfoProperty =
        DependencyProperty.Register(
            nameof(TechnicalInfo),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(
            nameof(BadgeText),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PreviewTextProperty =
        DependencyProperty.Register(
            nameof(PreviewText),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata("?"));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Brush),
            typeof(ToolAssetHeader),
            new PropertyMetadata(Brushes.DimGray));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(
            nameof(BadgeBackground),
            typeof(Brush),
            typeof(ToolAssetHeader),
            new PropertyMetadata(Brushes.DimGray));

    public static readonly DependencyProperty PreviewBackgroundProperty =
        DependencyProperty.Register(
            nameof(PreviewBackground),
            typeof(Brush),
            typeof(ToolAssetHeader),
            new PropertyMetadata(Brushes.DimGray));

    public ToolAssetHeader()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string TechnicalInfo
    {
        get => (string)GetValue(TechnicalInfoProperty);
        set => SetValue(TechnicalInfoProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public string PreviewText
    {
        get => (string)GetValue(PreviewTextProperty);
        set => SetValue(PreviewTextProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Brush BadgeBackground
    {
        get => (Brush)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public Brush PreviewBackground
    {
        get => (Brush)GetValue(PreviewBackgroundProperty);
        set => SetValue(PreviewBackgroundProperty, value);
    }
}