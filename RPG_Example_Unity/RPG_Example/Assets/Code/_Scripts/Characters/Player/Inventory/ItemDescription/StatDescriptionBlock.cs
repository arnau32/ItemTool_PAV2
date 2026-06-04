using System;
using static Enums;
using UnityEngine.UIElements;
using System.Diagnostics;

public enum StatType
{
    Attack,
    Defense,
    Health
}

[Serializable]
public class StatDescriptionBlock : ItemDescriptionBlock
{
    public StatType statType;
    public int value;

    protected override VisualElement CreateElement()
    {
        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;

        var icon = new VisualElement();
        icon.AddToClassList($"icon-{statType.ToString().ToLower()}");

        var label_value = new Label($"+{value}");
        label_value.AddToClassList("desc-text");
        label_value.style.fontSize = 40;

        var label_Text = new Label(statType.ToString());
        label_Text.AddToClassList("desc-text");
        label_Text.style.fontSize = 25;

        root.Add(icon);
        root.Add(label_value);
        root.Add(label_Text);

        return root;
    }
}