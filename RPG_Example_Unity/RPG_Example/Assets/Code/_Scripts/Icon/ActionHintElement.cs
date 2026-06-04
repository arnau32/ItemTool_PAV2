using UnityEngine;
using UnityEngine.UIElements;

// Visual element for a single input hint (icon + label, or two icons + label).
public class ActionHintElement : VisualElement
{
    private readonly VisualElement _iconA;
    private readonly VisualElement _iconB; // null if single-icon constructor used
    private readonly Label _label;
    private readonly VisualElement _iconContainer;

    // ── Single icon constructor ───────────────────────────────────────────────

    public ActionHintElement(Sprite sprite, string text)
    {
        AddToClassList("action-hint");

        _iconContainer = new VisualElement();
        _iconContainer.AddToClassList("action-hint-icon-container");
        Add(_iconContainer);

        _iconA = new VisualElement();
        _iconA.AddToClassList("action-hint-icon");
        _iconContainer.Add(_iconA);

        // _iconB stays null for single-icon hints.

        _label = new Label(text);
        _label.AddToClassList("action-hint-label");
        Add(_label);

        if (sprite != null)
            _iconA.style.backgroundImage = new StyleBackground(sprite);
    }

    // ── Two icon constructor ──────────────────────────────────────────────────

    public ActionHintElement(Sprite spriteA, Sprite spriteB, string text)
    {
        AddToClassList("action-hint");

        _iconContainer = new VisualElement();
        _iconContainer.AddToClassList("action-hint-icon-container");
        Add(_iconContainer);

        _iconA = new VisualElement();
        _iconA.AddToClassList("action-hint-icon");
        _iconContainer.Add(_iconA);

        _iconB = new VisualElement();
        _iconB.AddToClassList("action-hint-icon");
        _iconContainer.Add(_iconB);

        _label = new Label(text);
        _label.AddToClassList("action-hint-label");
        Add(_label);

        if (spriteA != null) _iconA.style.backgroundImage = new StyleBackground(spriteA);
        if (spriteB != null) _iconB.style.backgroundImage = new StyleBackground(spriteB);
    }

    public void UpdateContent(Sprite spriteA, Sprite spriteB, string text)
    {
        // Label — string.== on interned literals is a pointer compare, zero alloc.
        if (_label.text != text)
            _label.text = text;

        // Icon A
        if (spriteA != null)
        {
            var bg = new StyleBackground(spriteA);
            // StyleBackground has no equality operator — always set.
            // This is one style write per frame when state changes, not every frame.
            _iconA.style.backgroundImage = bg;
            _iconA.style.display = DisplayStyle.Flex;
        }
        else
        {
            _iconA.style.display = DisplayStyle.None;
        }

        // Icon B — only available in the two-icon variant
        if (_iconB != null)
        {
            if (spriteB != null)
            {
                _iconB.style.backgroundImage = new StyleBackground(spriteB);
                _iconB.style.display = DisplayStyle.Flex;
            }
            else
            {
                _iconB.style.display = DisplayStyle.None;
            }
        }
    }

    public void SetVisible(bool visible)
    {
        style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}