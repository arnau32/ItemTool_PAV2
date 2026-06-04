using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ItemTool.App.Views.Shared;

[ContentProperty(nameof(SectionContent))]
public partial class ToolSection : UserControl
{
    public static readonly DependencyProperty SectionTitleProperty =
        DependencyProperty.Register(
            nameof(SectionTitle),
            typeof(string),
            typeof(ToolSection),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SectionContentProperty =
        DependencyProperty.Register(
            nameof(SectionContent),
            typeof(object),
            typeof(ToolSection),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(ToolSection),
            new PropertyMetadata(true));

    public ToolSection()
    {
        InitializeComponent();
    }

    public string SectionTitle
    {
        get => (string)GetValue(SectionTitleProperty);
        set => SetValue(SectionTitleProperty, value);
    }

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}