using System;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using UnityEngine;

[Serializable]
public class TextDescriptionBlock : ItemDescriptionBlock
{
    public LocalizedString text;

    protected override VisualElement CreateElement()
    {
        var label = new Label(text.GetLocalizedString());
        label.AddToClassList("desc-text");

        text.StringChanged += (value) =>
        {
            label.text = value;
        };

        return label;
    }
}
