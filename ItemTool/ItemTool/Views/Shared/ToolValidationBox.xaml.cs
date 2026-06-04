using System.Windows;
using System.Windows.Controls;

namespace ItemTool.App.Views.Shared;

public partial class ToolValidationBox : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ToolValidationBox),
            new PropertyMetadata("Validation"));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ToolValidationBox),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BoxHeightProperty =
        DependencyProperty.Register(
            nameof(BoxHeight),
            typeof(double),
            typeof(ToolValidationBox),
            new PropertyMetadata(75d));

    public ToolValidationBox()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double BoxHeight
    {
        get => (double)GetValue(BoxHeightProperty);
        set => SetValue(BoxHeightProperty, value);
    }
}