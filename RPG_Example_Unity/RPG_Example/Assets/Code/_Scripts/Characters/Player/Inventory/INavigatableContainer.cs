using System.Collections.Generic;
using UnityEngine;

public enum NavigationGroup
{
    Inventory = 0,
    Loot = 1,
    Equipment = 2,
    Consumable = 3,
}

public interface INavigatableContainer : IItemContainer
{
    int GetGridWidth();
    int GetGridHeight();

    // 获取某格子的 UI Rect（用于移动 cursor）
    Rect GetCellRect(int x, int y);

    // 某容器是否拥有可导航的特殊 slot，例如武器槽
    bool HasSpecialSlots();
    IReadOnlyList<Rect> GetSpecialSlotRects();

    NavigationGroup NavGroup { get; }
    int NavOrder { get; }
    bool IsActiveForNavigation();
}

