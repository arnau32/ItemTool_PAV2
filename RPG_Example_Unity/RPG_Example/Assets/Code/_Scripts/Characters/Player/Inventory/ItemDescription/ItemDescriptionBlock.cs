using System;
using UnityEngine.UIElements;

[Serializable]
public abstract class ItemDescriptionBlock
{
    public VisualElement CreateWrappedElement()
    {
        var wrapper = new VisualElement();
        wrapper.AddToClassList("desc-block-wrapper");

        var content = CreateElement();
        wrapper.Add(content);

        return wrapper;
    }

    protected abstract VisualElement CreateElement();
}