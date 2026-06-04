using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public static readonly DependencyProperty PreviewImageSourceProperty =
        DependencyProperty.Register(
            nameof(PreviewImageSource),
            typeof(ImageSource),
            typeof(ToolAssetHeader),
            new PropertyMetadata(null));

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

    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(
            nameof(SaveCommand),
            typeof(ICommand),
            typeof(ToolAssetHeader),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SaveButtonTextProperty =
        DependencyProperty.Register(
            nameof(SaveButtonText),
            typeof(string),
            typeof(ToolAssetHeader),
            new PropertyMetadata("Save"));

    public static readonly DependencyProperty SaveButtonVisibilityProperty =
        DependencyProperty.Register(
            nameof(SaveButtonVisibility),
            typeof(Visibility),
            typeof(ToolAssetHeader),
            new PropertyMetadata(Visibility.Collapsed));

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
    
    public ImageSource? PreviewImageSource
    {
        get => (ImageSource?)GetValue(PreviewImageSourceProperty);
        set => SetValue(PreviewImageSourceProperty, value);
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

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public string SaveButtonText
    {
        get => (string)GetValue(SaveButtonTextProperty);
        set => SetValue(SaveButtonTextProperty, value);
    }

    public Visibility SaveButtonVisibility
    {
        get => (Visibility)GetValue(SaveButtonVisibilityProperty);
        set => SetValue(SaveButtonVisibilityProperty, value);
    }
}