using UnityEngine;

public class NavigationNode
{
    public INavigatableContainer container;
    public int gridX;
    public int gridY;
    public Rect rect;        // worldBound rect
    public Vector2 center;
    public Vector2 worldCenter;
    public bool isSpecial;   // special slot or normal grid
}
