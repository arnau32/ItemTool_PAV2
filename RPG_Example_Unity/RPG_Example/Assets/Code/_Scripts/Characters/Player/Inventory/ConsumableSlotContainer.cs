using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class ConsumableSlotContainer : MonoBehaviour, INavigatableContainer
{
    public Enums.ItemType acceptedType = Enums.ItemType.Consumable;
    public int slotNumber;
    public NavigationGroup NavGroup => NavigationGroup.Consumable;
    public int NavOrder => slotNumber;

    public bool IsActiveForNavigation() => true;


    public VisualElement m_Root;
    public VisualElement m_SlotGrid;
    private VisualElement m_Telegraph;

    public ItemStack currentStack;

    private bool m_IsEquip;

    private void Start()
    {
        Configure();
    }
    private void Update()
    {
        if (m_Telegraph == null)
        {
            return;
        }

        if (ContainerRegistry._currentlyDragging == null)
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
        }
    }
    private async void Configure()
    {
        m_Root     = UIManager.Instance.root;
        m_SlotGrid = m_Root.Q<VisualElement>($"Slot_Consumable_{slotNumber}");

        await UniTask.WaitForEndOfFrame();

        for (int i = m_SlotGrid.childCount - 1; i >= 0; i--)
        {
            var child = m_SlotGrid[i];
            if (child.name != "Slot_Boder")
                child.RemoveFromHierarchy();
        }

        ConfigureTelegraph();

        currentStack = null;
        m_IsEquip    = false;

        ContainerRegistry.Register((IItemContainer)this);
        ContainerRegistry.Register(this);
        NavigationRegistry.Register(this);

        RestoreFromSave();
    }

    /// Reads SaveData for this slot number and spawns the ItemStack + ItemVisual.
    /// Called at the end of Configure() once m_SlotGrid is ready.
    private void RestoreFromSave()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        var data = save.CurrentSave;
        ConsumableSlotSaveData entry = default;
        bool found = false;

        foreach (var consumableSlotSaveData in data.inventory.consumableSlots)
        {
            if (consumableSlotSaveData.slotNumber != slotNumber) continue;
            
            entry = consumableSlotSaveData;
            found = true;
            break;
        }

        if (!found) return;

        var itemData = save.ResolveItem(entry.uniqueID);
        if (itemData == null) return;

        var stack = new ItemStack
        {
            data     = itemData,
            quantity = entry.quantity,
            gridX    = 0,
            gridY    = 0
        };

        var visual = new ItemVisual(stack, this);
        visual.style.visibility = Visibility.Visible;
        visual.OriginContainer   = this;
        m_SlotGrid.Add(visual);
        stack.RootVisual = visual;

        currentStack = stack;
        m_IsEquip    = true;

        currentStack.RootVisual.style.height = 75;
        currentStack.RootVisual.style.width = 75;
    }

    private void ConfigureTelegraph()
    {
        m_Telegraph = new VisualElement();
        m_Telegraph.AddToClassList("slot-icon-highlighted");

        m_Telegraph.style.position = Position.Absolute;
        m_Telegraph.style.visibility = Visibility.Hidden;

        m_SlotGrid.Add(m_Telegraph);
    }

    public int GetGridWidth() => 1;
    public int GetGridHeight() => 1;
    public Rect GetCellRect(int x, int y)
    {
        return m_SlotGrid.worldBound;
    }

    public bool HasSpecialSlots() => true;

    public IReadOnlyList<Rect> GetSpecialSlotRects()
    {
        return new List<Rect> {
        m_SlotGrid.worldBound
    };
    }

    public async void AutoTransferItem(ItemStack itemStack)
    {
        if (TabViewManager.Instance.m_TabType != TabType.Inventory || PlayerInventory.Instance == null) return;


        var visual = itemStack.RootVisual;
        if (visual == null) return;

        this.RemoveStack(itemStack);
        m_SlotGrid.Remove(visual);


        visual.OriginContainer.HideTelegraph();

        ContainerRegistry.TransferVisualBetweenContainers(itemStack, PlayerInventory.Instance.GetInventoryGrid(), itemStack.gridX, itemStack.gridY);
        bool placedSuccessfully = await PlayerInventory.Instance.AutoPlaceItem(visual);

        if (!placedSuccessfully)
        {
            AddStack(itemStack);
            m_SlotGrid.Add(visual);

            visual.OriginContainer = this;
            visual.RestoreOriginal();
            return;
        }

        ContainerRegistry.TransferVisualBetweenContainers(itemStack, PlayerInventory.Instance.GetInventoryGrid(), itemStack.gridX, itemStack.gridY);
        PlayerInventory.Instance.AddStack(itemStack);

        visual.OriginContainer = PlayerInventory.Instance;

        visual.style.visibility = Visibility.Visible;
    }

    public void AddStack(ItemStack stack)
    {
        currentStack = stack; 
        m_IsEquip = true;
        stack.RootVisual.OriginContainer = this;
    }

    public void RemoveStack(ItemStack stack)
    {
        if (currentStack != stack) return;
        
        currentStack = null;
        m_IsEquip = false;
    }
    public ItemStack GetStackAt(int x, int y) => currentStack;
    
    public (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint) => (0, 0);

    public (bool canPlace, Vector2 position) ShowPlacementTarget(ItemVisual draggedItem)
    {
        if (draggedItem == null || m_IsEquip && draggedItem.m_Stack != currentStack) return (false, Vector2.zero);

        if (draggedItem.m_Stack.data.itemType != Enums.ItemType.Consumable || !draggedItem.worldBound.Overlaps(m_SlotGrid.worldBound))
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
            return (false, Vector2.zero);
        }

        m_Telegraph.style.width = m_SlotGrid.resolvedStyle.width;
        m_Telegraph.style.height = m_SlotGrid.resolvedStyle.height;

        m_Telegraph.style.left = 0;
        m_Telegraph.style.top = 0;
        m_Telegraph.style.visibility = Visibility.Visible;

        return (true, m_SlotGrid.worldBound.position);
    }
    
    public ContainerType GetContainerType() => ContainerType.SlotConsum;

    public void BeginDrag(ItemVisual dragged)
    {
        ContainerRegistry._currentlyDragging = dragged;
    }

    public void EndDrag()
    {
        ContainerRegistry._currentlyDragging = null;
        m_Telegraph.style.visibility = Visibility.Hidden;
        
        if (currentStack == null) return;
        
        currentStack.RootVisual.ApplyRotation(0, revertOnFail: false);
        currentStack.gridX = 0;
        currentStack.gridY = 0;
    }

    public Task<bool> AutoPlaceItem(ItemVisual item)
    {
        if (item == null) return Task.FromResult(false);

        var stack = item.m_Stack;

        // ��Ϊ��
        if (currentStack == null)
        {
            AddStack(stack);

            stack.gridX = 0;
            stack.gridY = 0;

            m_SlotGrid.Add(item);

            item.OriginContainer = this;

            return Task.FromResult(true);
        }

        // Same type — merge up to maxStack
        bool sameType =
            currentStack.data == stack.data ||
            currentStack.data.uniqueID == stack.data.uniqueID;

        if (!sameType) return Task.FromResult(false);

        int canAdd = currentStack.data.maxStack - currentStack.quantity;
        if (canAdd <= 0) return Task.FromResult(false);

        int toAdd = Mathf.Min(canAdd, stack.quantity);
        currentStack.quantity += toAdd;
        currentStack.RootVisual.UpdateCountLabel();
        stack.quantity -= toAdd;

        if (stack.quantity <= 0)
        {
            stack.RootVisual.OriginContainer.RemoveItemStack(stack);
            return Task.FromResult(true);
        }

        // Partial fill — overflow stays in origin
        stack.RootVisual?.UpdateCountLabel();
        return Task.FromResult(false);

        // �������Զ��滻
    }

    public ItemStack GetSameTypeOverlapItem(ItemVisual draggedItem)
    {
        if (currentStack == null) return null;

        bool sameType =
            currentStack.data == draggedItem.m_Stack.data ||
            currentStack.data.uniqueID == draggedItem.m_Stack.data.uniqueID;

        if (sameType) return currentStack;

        return null;
    }


    public void RemoveItemStack(ItemStack stack)
    {
        RemoveStack(stack);
        RemoveItemFromInventoryGrid(stack.RootVisual);
    }

    private void RemoveItemFromInventoryGrid(VisualElement item)
    {
        if (item != null && item.parent == m_SlotGrid)
        {
            m_SlotGrid.Remove(item);
        }
    }

    public bool ContainsWorldPoint(Vector2 worldPoint) => m_SlotGrid.worldBound.Contains(worldPoint);

    public VisualElement GetInventoryGrid() => m_SlotGrid;

    public VisualElement GetTelegraph() => m_Telegraph;
}