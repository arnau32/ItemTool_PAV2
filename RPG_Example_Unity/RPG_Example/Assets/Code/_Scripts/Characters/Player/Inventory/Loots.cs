using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class Loots : MonoBehaviour, INavigatableContainer
{
    public enum LoadMode
    {
        UseSavedGridPosition,
        AutoPlaceSequentially
    }

    private LoadMode loadMode;
    public bool IsActiveForNavigation() => isActive;
    public NavigationGroup NavGroup => NavigationGroup.Loot;
    public int NavOrder => 0;

    // UI references
    private VisualElement m_Root;
    public VisualElement m_LootBox;
    private VisualElement m_LootGrid;

    public bool isActive;

    public PlayerInventory playerInventory;
    private VisualElement m_Telegraph;
    private LootManager _lootManager;

    public StorageType _type;

    public VisualElement GetTelegraph()
    {
        return m_Telegraph;
    }

    public VisualElement GetInventoryGrid()
    {
        return m_LootGrid;
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

    public ItemStack GetStackAt(int x, int y)
    {
        foreach (var stack in ItemStacks)
        {
            if (stack.RootVisual == null) continue;

            if (stack == ContainerRegistry._currentlyDragging?.m_Stack)
                continue;

            int w = stack.RootVisual.GetSlotWidth();
            int h = stack.RootVisual.GetSlotHeight();

            int sx = stack.gridX;
            int sy = stack.gridY;

            if (x >= sx && x < sx + w &&
                y >= sy && y < sy + h)
            {
                return stack;
            }
        }

        return null;
    }


    public int GetGridWidth() => InventoryDimensions.Width;
    public int GetGridHeight() => InventoryDimensions.Height;

    public Rect GetCellRect(int x, int y)
    {
        float px = m_LootGrid.worldBound.x + x * SlotDimension.Width;
        float py = m_LootGrid.worldBound.y + y * SlotDimension.Height;

        return new Rect(px, py, SlotDimension.Width, SlotDimension.Height);
    }

    public bool HasSpecialSlots() => false;

    public IReadOnlyList<Rect> GetSpecialSlotRects() => null;


    // Slot pixel size
    public static Dimensions SlotDimension { get; private set; }


    // Inventory & layout data
    public List<ItemStack> ItemStacks = new List<ItemStack>();

    public Dimensions InventoryDimensions;

    private bool m_IsConfig;


    void Start()
    {
        GameServices.TryGet(out _lootManager);
        Configure();
        LoadItems(ItemStacks);
    }

    /// Initialize UI references, wait layout, measure slot, setup telegraph
    private async void Configure()
    {
        m_Root = UIManager.Instance.root;
        m_Root.focusable = true;
        m_LootBox = m_Root.Q<VisualElement>("Loot");
        m_LootGrid = m_Root.Q<VisualElement>("Grid_2");
        loadMode = LoadMode.AutoPlaceSequentially;

        await UniTask.WaitForEndOfFrame();

        ConfigureInventoryTelegraph();
        ConfigureSlotDimensions();

        ContainerRegistry.Register(this);
        m_IsConfig = true;

        m_LootBox.style.display = DisplayStyle.None;
        isActive = false;

        _type = StorageType.Bag;
    }

    public async void OpenLoots(List<ItemStack> sourceItems, StorageType type)
    {
        ClearAllStacks();

        List<ItemStack> copy = sourceItems.Select(i => i.Clone()).ToList();
        SetSource(sourceItems);
        ItemStacks = copy;

        m_LootBox.style.display = DisplayStyle.Flex;

        isActive = true;

        _type = type;

        TabviewLocalization.Instance.RefreshTexts();

        loadMode = LoadMode.UseSavedGridPosition;
        LoadItems(ItemStacks);

        // ⭐ 关键：等一帧，等 worldBound 稳定
        await UniTask.WaitForEndOfFrame();

        NavigationRegistry.Register(this);
    }


    public void CloseLoots()
    {
        if (isActive)
        {
            ApplyChangesToSource();

            Vector3 playerPos = playerInventory?.player != null ? playerInventory.player.transform.position : Vector3.zero;
            for (int i = 0; i < ItemStacks.Count; i++)
            {
                var s = ItemStacks[i];
                if (s?.data == null) continue;
                string ignoredId = !string.IsNullOrEmpty(s.data.uniqueID) ? s.data.uniqueID : s.data.itemNameID;
                CombatAnalytics.LootIgnored(ignoredId, s.data.itemType.ToString(), s.data.value, s.quantity, playerPos);
            }

            ClearAllStacks();

            NavigationRegistry.Unregister(this);

            m_LootBox.style.display = DisplayStyle.None;
            isActive = false;
        }
        else
        {
            if (ItemStacks.Count > 0)
            {
                DropAllToWorld();
            }

            ClearAllStacks();
            NavigationRegistry.Unregister(this);
            m_LootBox.style.display = DisplayStyle.None;
            isActive = false;
        }
    }

    public void DropAllToWorld()
    {
        if (ItemStacks.Count == 0) return;

        Vector3 dropPos = playerInventory.player.transform.position;

        _lootManager?.SpawnLootFromStacks(
            ItemStacks.Select(s => s.Clone()).ToList(),
            dropPos
        );

        ClearAllStacks();
    }


    // Loads stored items into UI, placing each if possible
    public async void LoadItems(List<ItemStack> ItemStacks)
    {
        await UniTask.WaitUntil(() => m_IsConfig);

        foreach (ItemStack itemStacks in ItemStacks)
        {
            ItemVisual inventoryItemVisual = new ItemVisual(itemStacks, this);
            AddItemToInventoryGrid(inventoryItemVisual);

            bool placedSuccessfully = false;

            if (loadMode == LoadMode.UseSavedGridPosition)
            {
                placedSuccessfully = await PlaceItemByGridPosition(
                    inventoryItemVisual,
                    itemStacks.gridX,
                    itemStacks.gridY
                );
                if (!placedSuccessfully)
                    placedSuccessfully = await AutoPlaceItem(inventoryItemVisual);
            }
            else
            {
                placedSuccessfully = await AutoPlaceItem(inventoryItemVisual);
            }

            inventoryItemVisual.ApplyRotation(itemStacks.rotationIndex, revertOnFail: false);

            if (!placedSuccessfully)
            {
                Debug.LogWarning(
                    $"Cannot place item {itemStacks.data.itemNameID} using mode {loadMode}"
                );
                RemoveItemFromInventoryGrid(inventoryItemVisual);
                continue;
            }

            ConfigureInventoryItem(itemStacks, inventoryItemVisual);
        }
    }

    public async Task<bool> AutoPlaceItem(ItemVisual item)
    {
        item.style.visibility = Visibility.Hidden;

        int originalRotation = item.m_Stack.rotationIndex;

        for (int r = 0; r < 4; r++)
        {
            int testRotation = (originalRotation + r) % 4;

            item.ApplyRotation(testRotation, revertOnFail: false);

            int w = item.GetSlotWidth();
            int h = item.GetSlotHeight();

            for (int y = 0; y < InventoryDimensions.Height; y++)
            {
                for (int x = 0; x < InventoryDimensions.Width; x++)
                {
                    bool success = await PlaceItemByGridPosition(item, x, y);

                    if (!success) continue;

                    item.m_Stack.gridX = x;
                    item.m_Stack.gridY = y;
                    item.m_Stack.rotationIndex = testRotation;

                    item.style.visibility = Visibility.Visible;
                    return true;
                }
            }
        }

        item.ApplyRotation(originalRotation, false);
        item.style.visibility = Visibility.Visible;

        return false;
    }

    private void ConfigureInventoryItem(ItemStack item, ItemVisual visual)
    {
        item.RootVisual = visual;
        visual.style.visibility = Visibility.Visible;
    }


    private void ConfigureSlotDimensions()
    {
        VisualElement firstSlot = m_LootGrid.Children().First();
        var measured = new Dimensions
        {
            Width = Mathf.RoundToInt(firstSlot.worldBound.width),
            Height = Mathf.RoundToInt(firstSlot.worldBound.height)
        };

        // Keep own static in sync (used by GetCellRect, PlaceItemByGridPosition, WorldPointToGrid).
        SlotDimension = measured;
        // Push to registry so ItemVisual and ContainerRegistry read the same value.
        ContainerRegistry.SetMeasuredSlotDimension(measured);
    }


    private Task<bool> PlaceItemByGridPosition(VisualElement newItem, int gridX, int gridY)
    {
        var itemVisual = (ItemVisual)newItem;
        int itemW = itemVisual.GetSlotWidth();
        int itemH = itemVisual.GetSlotHeight();

        if (gridX < 0 || gridY < 0 ||
            gridX + itemW > InventoryDimensions.Width ||
            gridY + itemH > InventoryDimensions.Height)
        {
            return Task.FromResult(false);
        }

        // Check logical grid overlap before moving the visual — avoids UIToolkit
        // layout.Overlaps() which lags one frame behind and causes stacked placements.
        foreach (var stack in ItemStacks)
        {
            if (stack.RootVisual == null) continue;
            if (stack.RootVisual.parent == null) continue;
            if (stack.RootVisual == newItem) continue;

            if (GridRectOverlap(gridX, gridY, itemW, itemH, stack.gridX, stack.gridY, stack.RootVisual.GetSlotWidth(), stack.RootVisual.GetSlotHeight()))
            {
                return Task.FromResult(false);
            }
        }

        SetItemPosition(newItem, new Vector2(
            SlotDimension.Width * gridX,
            SlotDimension.Height * gridY));

        return Task.FromResult(true);
    }


    private void ConfigureInventoryTelegraph()
    {
        m_Telegraph = new VisualElement
        {
            name = "Telegraph",
            style =
            {
                position = Position.Absolute,
                visibility = Visibility.Hidden
            }
        };

        m_Telegraph.AddToClassList("slot-icon-highlighted");
        AddItemToInventoryGrid(m_Telegraph);
    }

    public async void AutoTransferItem(ItemStack itemStack)
    {
        if ((TabViewManager.Instance.m_TabType != TabType.Inventory) || playerInventory == null) return;

        var visual = itemStack.RootVisual;
        if (visual == null) return;

        visual.SetOriginalRotateIndex(visual._rotationIndex);

        // ── Consumables: auto-equip to DPAD before falling back to inventory ─
        if (itemStack.data.itemType == Enums.ItemType.Consumable)
        {
            // Step 1: stack into occupied DPAD slots that hold the same item, respecting maxStack.
            for (int i = 0; i < 4 && itemStack.quantity > 0; i++)
            {
                var dpad = ContainerRegistry.GetConsumableSlot(i);
                if (dpad?.currentStack?.data == null) continue;

                // Reference comparison first — uniqueID can be null/empty for consumables.
                bool sameType = dpad.currentStack.data == itemStack.data ||
                                (!string.IsNullOrEmpty(dpad.currentStack.data.uniqueID) &&
                                 dpad.currentStack.data.uniqueID == itemStack.data.uniqueID);
                if (!sameType) continue;

                int canAdd = dpad.currentStack.data.maxStack - dpad.currentStack.quantity;
                if (canAdd <= 0) continue;

                int toAdd = Mathf.Min(canAdd, itemStack.quantity);
                dpad.currentStack.quantity += toAdd;
                dpad.currentStack.RootVisual?.UpdateCountLabel();
                itemStack.quantity -= toAdd;
                visual.UpdateCountLabel();
            }

            if (itemStack.quantity <= 0)
            {
                RemoveItemStack(itemStack);
                visual.RemoveFromHierarchy();
                visual.CleanupRotatedAssets();
                LogPickup(itemStack);
                return;
            }

            // Step 2: place remainder into first valid empty DPAD slot.
            // Guard m_SlotGrid: if null the slot isn't configured yet — skip it to avoid
            // silently orphaning the item inside an async void context.
            for (int i = 0; i < 4; i++)
            {
                var dpad = ContainerRegistry.GetConsumableSlot(i);
                if (dpad?.m_SlotGrid == null || dpad.currentStack != null) continue;

                RemoveItemStack(itemStack);            // removes from ItemStacks + loot grid
                itemStack.gridX = 0;
                itemStack.gridY = 0;
                dpad.m_SlotGrid.Add(visual);           // UIToolkit transfers parent automatically
                visual.ApplyRotation(0, revertOnFail: false);
                // Reset position inherited from the loot grid — slots use (0,0) origin.
                visual.style.left   = 0;
                visual.style.top    = 0;
                visual.style.width  = 75;
                visual.style.height = 75;
                visual.style.visibility = Visibility.Visible;
                dpad.AddStack(itemStack);

                LogPickup(itemStack);
                return;
            }

            // Step 3: DPAD full — fall through to inventory with remaining quantity.
        }
        // ── End DPAD block ─────────────────────────────────────────────────

        RemoveStack(itemStack);
        RemoveItemFromInventoryGrid(visual);

        visual.OriginContainer.HideTelegraph();

        ItemStack stackTarget = playerInventory.FindStackForAutoStack(itemStack);

        if (stackTarget != null)
        {
            int canAdd = stackTarget.data.maxStack - stackTarget.quantity;
            int toMove = Mathf.Min(canAdd, itemStack.quantity);

            stackTarget.quantity += toMove;
            itemStack.quantity -= toMove;

            stackTarget.RootVisual.UpdateCountLabel();

            playerInventory.NotifyItemArrived(stackTarget.data, toMove);

            if (itemStack.quantity <= 0)
            {
                LogPickup(itemStack);
                visual.RemoveFromHierarchy();
                visual.CleanupRotatedAssets();
                return;
            }

            visual.UpdateCountLabel();
        }

        visual.SetOriginalPosition(visual.worldBound.position);

        var oldGrid = visual.parent;
        if (oldGrid != null) oldGrid.Remove(visual);
        playerInventory.GetInventoryGrid().Add(visual);

        bool placedSuccessfully = await playerInventory.AutoPlaceItem(visual);

        if (!placedSuccessfully)
        {
            // DPAD and inventory both full — restore to loot and signal error.
            playerInventory.GetInventoryGrid().Remove(visual);
            this.AddStack(itemStack);
            AddItemToInventoryGrid(visual);
            visual.OriginContainer = this;
            visual.RestoreOriginal();
            InventoryCursorManager.Instance.FlashRed();
            visual.style.visibility = Visibility.Visible;
            return;
        }

        playerInventory.AddStack(itemStack);
        visual.OriginContainer = playerInventory;
        visual.style.visibility = Visibility.Visible;

        LogPickup(itemStack);
    }

    private void LogPickup(ItemStack stack)
    {
        if (stack?.data == null) return;
        Vector3 pos = playerInventory?.player != null ? playerInventory.player.transform.position : Vector3.zero;
        string id = !string.IsNullOrEmpty(stack.data.uniqueID) ? stack.data.uniqueID : stack.data.itemNameID;
        CombatAnalytics.LootPickedUp(id, stack.data.itemType.ToString(), stack.data.value, stack.quantity, pos);
    }

    public bool CanFitItem(ItemVisual item)
    {
        int w = item.GetSlotWidth();
        int h = item.GetSlotHeight();

        for (int y = 0; y < InventoryDimensions.Height; y++)
        {
            for (int x = 0; x < InventoryDimensions.Width; x++)
            {
                // 边界检查
                if (x + w > InventoryDimensions.Width ||
                    y + h > InventoryDimensions.Height)
                    continue;

                bool overlap = false;
                foreach (var stack in ItemStacks)
                {
                    if (stack.RootVisual == null) continue;

                    if (GridRectOverlap(
                            x, y, w, h,
                            stack.gridX,
                            stack.gridY,
                            stack.RootVisual.GetSlotWidth(),
                            stack.RootVisual.GetSlotHeight()))
                    {
                        overlap = true;
                        break;
                    }
                }

                if (!overlap)
                    return true;
            }
        }

        return false;
    }

    public ItemStack FindStackForAutoStack(ItemStack incoming)
    {
        foreach (var stack in ItemStacks)
        {
            if (stack.data.uniqueID != incoming.data.uniqueID)
                continue;

            if (stack.quantity < stack.data.maxStack)
                return stack;
        }

        return null;
    }

    public (bool canPlace, Vector2 position) ShowPlacementTarget(ItemVisual draggedItem)
    {
        Rect gridRect = m_LootGrid.worldBound;
        Rect cursorRect = InventoryCursorManager.Instance.GetCurrentCellRect();

        Rect realRect = new Rect(cursorRect.position, draggedItem.layout.size);

        bool fullyInside = realRect.xMin >= gridRect.xMin &&
                           realRect.yMin >= gridRect.yMin &&
                           realRect.xMax - 20 <= gridRect.xMax &&
                            realRect.yMax - 20 <= gridRect.yMax;

        if (!fullyInside)
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
            return (false, Vector2.zero);
        }

        VisualElement targetSlot = m_LootGrid.Children()
            .Where(x => x != draggedItem && x.layout.Overlaps(draggedItem.layout))
            .OrderBy(x => Vector2.Distance(x.worldBound.position, draggedItem.worldBound.position))
            .FirstOrDefault();

        if (targetSlot == null)
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
            return (false, Vector2.zero);
        }

        m_Telegraph.style.width = draggedItem.style.width;
        m_Telegraph.style.height = draggedItem.style.height;

        SetItemPosition(m_Telegraph, new Vector2(targetSlot.layout.position.x, targetSlot.layout.position.y));
        m_Telegraph.style.visibility = Visibility.Visible;


        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_LootGrid.worldBound.position;

        // ★ 换成 RoundToInt 以避免错格
        int tgx = Mathf.RoundToInt(telePosLocal.x / SlotDimension.Width);
        int tgy = Mathf.RoundToInt(telePosLocal.y / SlotDimension.Height);

        int tw = draggedItem.GetSlotWidth();
        int th = draggedItem.GetSlotHeight();

        bool hasHardOverlap = false;
        ItemStack overlapCandidate = null;

        foreach (var stack in ItemStacks)
        {
            if (stack.RootVisual == null) continue;
            if (stack == draggedItem.m_Stack) continue;

            int gx = stack.gridX;
            int gy = stack.gridY;
            int gw = stack.RootVisual.GetSlotWidth();
            int gh = stack.RootVisual.GetSlotHeight();

            if (!GridRectOverlap(tgx, tgy, tw, th, gx, gy, gw, gh)) continue;

            hasHardOverlap = true;
            overlapCandidate = stack;
            break;
        }

        if (hasHardOverlap)
        {
            if ((overlapCandidate.data == draggedItem.m_Stack.data ||
                 overlapCandidate.data.uniqueID == draggedItem.m_Stack.data.uniqueID) && draggedItem.m_Item.maxStack > 1)
            {
                // Same type, telegraph ON
                m_Telegraph.style.visibility = Visibility.Visible;
            }
            else
            {
                // Block placement
                m_Telegraph.style.visibility = Visibility.Hidden;
            }

            return (false, Vector2.zero);
        }

        return (true, targetSlot.worldBound.position);
    }


    private bool GridRectOverlap(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }

    public ItemStack GetSameTypeOverlapItem(ItemVisual draggedItem)
    {
        if (draggedItem == null) return null;

        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_LootGrid.worldBound.position;

        int tgx = Mathf.RoundToInt(telePosLocal.x / SlotDimension.Width);
        int tgy = Mathf.RoundToInt(telePosLocal.y / SlotDimension.Height);

        int tw = draggedItem.GetSlotWidth();
        int th = draggedItem.GetSlotHeight();

        foreach (var stack in ItemStacks)
        {
            if (stack.RootVisual == null) continue;
            if (stack == draggedItem.m_Stack) continue;

            int gx = stack.gridX;
            int gy = stack.gridY;
            int gw = stack.RootVisual.GetSlotWidth();
            int gh = stack.RootVisual.GetSlotHeight();

            if (GridRectOverlap(tgx, tgy, tw, th, gx, gy, gw, gh))
            {
                bool sameType =
                    stack.data == draggedItem.m_Stack.data ||
                    stack.data.uniqueID == draggedItem.m_Stack.data.uniqueID;

                if (sameType)
                    return stack;

                return null;
            }
        }

        return null;
    }

    public ItemStack GetSwapCandidate(ItemVisual draggedItem)
    {
        if (draggedItem == null || m_Telegraph == null) return null;

        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_LootGrid.worldBound.position;
        int tgx = Mathf.RoundToInt(telePosLocal.x / SlotDimension.Width);
        int tgy = Mathf.RoundToInt(telePosLocal.y / SlotDimension.Height);
        int tw  = draggedItem.GetSlotWidth();
        int th  = draggedItem.GetSlotHeight();

        ItemStack candidate = null;
        int overlapCount    = 0;

        foreach (var stack in ItemStacks)
        {
            if (stack.RootVisual == null) continue;
            if (stack == draggedItem.m_Stack) continue;

            int gx = stack.gridX;
            int gy = stack.gridY;
            int gw = stack.RootVisual.GetSlotWidth();
            int gh = stack.RootVisual.GetSlotHeight();

            if (!GridRectOverlap(tgx, tgy, tw, th, gx, gy, gw, gh)) continue;

            overlapCount++;
            if (overlapCount > 1) return null;

            if (gw == tw && gh == th)
                candidate = stack;
        }

        return candidate;
    }


    private static void SetItemPosition(VisualElement element, Vector2 vector)
    {
        element.style.left = vector.x;
        element.style.top = vector.y;
    }

    public void AddStack(ItemStack stack)
    {
        ItemStacks.Add(stack);
        stack.RootVisual.OriginContainer = this;
    }

    public void RemoveStack(ItemStack stack)
    {
        ItemStacks.Remove(stack);
    }

    private void AddItemToInventoryGrid(VisualElement item)
    {
        m_LootGrid.Add(item);
        item.BringToFront();
    }

    public void RemoveItemStack(ItemStack itemStack)
    {
        ItemStacks.Remove(itemStack);
        RemoveItemFromInventoryGrid(itemStack.RootVisual);
    }

    private void RemoveItemFromInventoryGrid(VisualElement item)
    {
        if (item != null && item.parent == m_LootGrid)
        {
            m_LootGrid.Remove(item);
        }
    }

    public void ClearAllStacks()
    {
        for (int i = ItemStacks.Count - 1; i >= 0; i--)
        {
            RemoveItemStack(ItemStacks[i]);
        }

        ItemStacks = new List<ItemStack>();
    }

    private List<ItemStack> originalSource;

    public void SetSource(List<ItemStack> source)
    {
        originalSource = source;
    }

    public void ApplyChangesToSource()
    {
        if (originalSource == null) return;

        originalSource.Clear();

        foreach (var stack in ItemStacks)
        {
            originalSource.Add(stack.Clone());
        }

        originalSource = null;
    }


    // called by ItemVisual when drag starts
    public void BeginDrag(ItemVisual dragged)
    {
        ContainerRegistry._currentlyDragging = dragged;
    }

    // called by ItemVisual when drag ends
    public void EndDrag()
    {
        ContainerRegistry._currentlyDragging = null;
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (m_LootGrid == null) return false;
        return m_LootGrid.worldBound.Contains(worldPoint);
    }

    public (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint)
    {
        Vector2 local = worldPoint - m_LootGrid.worldBound.position;

        float fx = local.x / ContainerRegistry.SlotDimension.Width;
        float fy = local.y / ContainerRegistry.SlotDimension.Height;

        int gx = Mathf.RoundToInt(fx);
        int gy = Mathf.RoundToInt(fy);

        return (gx, gy);
    }

    public ContainerType GetContainerType()
    {
        return ContainerType.Grids;
    }
}