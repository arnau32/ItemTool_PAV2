using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.MessageBox;

public interface IItemContainer
{
    public (bool canPlace, Vector2 position) ShowPlacementTarget(ItemVisual draggedItem);

    public Task<bool> AutoPlaceItem(ItemVisual item);
    public void BeginDrag(ItemVisual dragged);
    public void EndDrag();

    void AddStack(ItemStack stack);
    void RemoveStack(ItemStack stack);

    void AutoTransferItem(ItemStack stack);
    public ItemStack GetSameTypeOverlapItem(ItemVisual draggedItem);
    public ItemStack GetSwapCandidate(ItemVisual draggedItem) => null;
    public void RemoveItemStack(ItemStack itemStack);
    bool ContainsWorldPoint(Vector2 worldPoint);

    (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint);

    public VisualElement GetInventoryGrid();
    public VisualElement GetTelegraph();

    public void HideTelegraph() 
    {
        GetTelegraph().style.visibility = Visibility.Hidden;
    }

    public ContainerType GetContainerType();

    public ItemStack GetStackAt(int x, int y);
}

public enum ContainerType
{
    Grids,
    SlotEquip,
    SlotConsum
}