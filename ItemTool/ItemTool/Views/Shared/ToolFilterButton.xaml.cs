using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ItemTool.App.Views.Shared;

public partial class ToolFilterButton : UserControl
{
    private static readonly Brush InactiveBackground = CreateBrush(58, 58, 58);
    private static readonly Brush InactiveBorder = CreateBrush(80, 80, 80);
    private static readonly Brush InactiveText = CreateBrush(220, 220, 220);
    private static readonly Brush ActiveText = Brushes.White;
    private static readonly Brush ActiveDot = Brushes.White;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ToolFilterButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(ToolFilterButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Brush),
            typeof(ToolFilterButton),
            new PropertyMetadata(Brushes.DimGray, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(ToolFilterButton),
            new PropertyMetadata(false, OnVisualPropertyChanged));

    public ToolFilterButton()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnVisualPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ToolFilterButton button)
            button.UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (ButtonRoot == null || LabelText == null || ColorDot == null)
            return;

        if (IsActive)
        {
            ButtonRoot.Background = AccentBrush;
            ButtonRoot.BorderBrush = AccentBrush;
            ButtonRoot.FontWeight = FontWeights.Bold;
            ButtonRoot.Opacity = 1.0;

            LabelText.Foreground = ActiveText;
            ColorDot.Background = ActiveDot;
        }
        else
        {
            ButtonRoot.Background = InactiveBackground;
            ButtonRoot.BorderBrush = InactiveBorder;
            ButtonRoot.FontWeight = FontWeights.Normal;
            ButtonRoot.Opacity = 0.85;

            LabelText.Foreground = InactiveText;
            ColorDot.Background = AccentBrush;
        }
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        SolidColorBrush brush = new(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}