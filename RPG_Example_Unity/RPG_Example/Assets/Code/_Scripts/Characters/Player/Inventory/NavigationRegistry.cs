using System.Collections.Generic;
using UnityEngine;

// Static registry for inventory navigation containers and nodes.

public static class NavigationRegistry
{
    public static List<INavigatableContainer>       Containers       = new();
    public static List<NavigationNode>              Nodes            = new();

    private static readonly List<INavigatableContainer> _orderedContainers = new();
    public  static List<INavigatableContainer>          OrderedContainers => _orderedContainers;

    public static void Reset()
    {
        Containers.Clear();
        Nodes.Clear();
        _orderedContainers.Clear();
    }

    public static void Register(INavigatableContainer c)
    {
        if (Containers.Contains(c)) return;

        Containers.Add(c);
        RebuildOrderedContainers();
        RebuildNodes();
    }

    public static void Unregister(INavigatableContainer c)
    {
        Containers.Remove(c);
        RebuildOrderedContainers();
        RebuildNodes();
    }

    public static void RebuildNodes()
    {
        Nodes.Clear();

        for (int ci = 0; ci < Containers.Count; ci++)
        {
            var c = Containers[ci];
            int w = c.GetGridWidth();
            int h = c.GetGridHeight();

            var grid = c.GetInventoryGrid();
            Vector2 gridWorldPos = grid.worldBound.position;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rect localRect = c.GetCellRect(x, y);

                    Vector2 worldCenter = gridWorldPos + localRect.center;

                    Nodes.Add(new NavigationNode
                    {
                        container = c,
                        gridX = x,
                        gridY = y,
                        rect = localRect,
                        center = localRect.center,
                        worldCenter = worldCenter,
                        isSpecial = false
                    });
                }

            if (c.HasSpecialSlots())
            {
                var special = c.GetSpecialSlotRects();

                for (int i = 0; i < special.Count; i++)
                {
                    Rect localRect = special[i];

                    Vector2 worldCenter = gridWorldPos + localRect.center;

                    Nodes.Add(new NavigationNode
                    {
                        container = c,
                        gridX = -1,
                        gridY = i,
                        rect = localRect,
                        center = localRect.center,
                        worldCenter = worldCenter,
                        isSpecial = true
                    });
                }
            }
        }
    }

    private static void RebuildOrderedContainers()
    {
        _orderedContainers.Clear();

        // Copy active containers into a temp buffer for sorting.
        foreach (var navigatableContainer in Containers)
        {
            if (navigatableContainer.IsActiveForNavigation())
            {
                _orderedContainers.Add(navigatableContainer);
            }
        }

        _orderedContainers.Sort(ContainerComparer.Instance);
    }

    // Reusable IComparer to avoid lambda allocation on Sort.
    private sealed class ContainerComparer : IComparer<INavigatableContainer>
    {
        public static readonly ContainerComparer Instance = new();

        public int Compare(INavigatableContainer a, INavigatableContainer b)
        {
            int groupCmp = a.NavGroup.CompareTo(b.NavGroup);
            return groupCmp != 0 ? groupCmp : a.NavOrder.CompareTo(b.NavOrder);
        }
    }
}