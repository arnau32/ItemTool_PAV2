using System.Collections.Generic;
using UnityEngine;

public enum InputIconType
{
    // Face buttons
    ButtonSouth,   // A (Xbox) / Cross (PS)
    ButtonEast,    // B / Circle
    ButtonWest,    // X / Square
    ButtonNorth,   // Y / Triangle

    // Shoulder
    LT,
    RT,

    // Sticks_CLK
    LeftJS_CLK, 
    RightJS_CLK,

    // D-pad
    D_pad,

    Home,
    Menu,

    // Shoulder
    LB,
    RB,


    // Sticks
    LeftJS,
    RightJS,
}
[CreateAssetMenu(menuName = "UI/Icon Database")]
public class IconDatabase : ScriptableObject
{
    [System.Serializable]
    public struct IconEntry
    {
        public InputIconType type;
        public Sprite sprite;
    }

    public List<IconEntry> icons;

    private Dictionary<InputIconType, Sprite> _lookup;

    public void Initialize()
    {
        _lookup = new Dictionary<InputIconType, Sprite>();
        foreach (var entry in icons)
        {
            _lookup[entry.type] = entry.sprite;
        }
    }

    public Sprite Get(InputIconType type)
    {
        if (_lookup == null)
            Initialize();

        return _lookup.TryGetValue(type, out var sprite) ? sprite : null;
    }
}

