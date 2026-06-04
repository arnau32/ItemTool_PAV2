using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class EquipmentSlotContainer : MonoBehaviour, INavigatableContainer
{
    public static event System.Action OnEquipmentChanged;

    public Enums.EquipSlot slotType;

    public NavigationGroup NavGroup => NavigationGroup.Equipment;

    // Weapon = 0, Helmet = 1, Armor = 2
    public int NavOrder => (int)slotType;

    public bool IsActiveForNavigation() => true;

    private VisualElement m_Root;
    private VisualElement m_SlotGrid;
    private VisualElement m_Telegraph;

    public ItemStack EquippedStack;

    public PlayerController playerController;
    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;
    private EquipmentHandler _equipmentHandler;

    public Dimensions DefaultSlotSize = new Dimensions { Width = 1, Height = 1 };
    private int PixelWidth(int slots) => ContainerRegistry.SlotDimension.Width * slots;
    private int PixelHeight(int slots) => ContainerRegistry.SlotDimension.Height * slots;

    private bool m_IsEquip;

    private void Start()
    {
        Configure();
    }

    private void Update()
    {
        if (m_Telegraph == null) return;

        if (ContainerRegistry._currentlyDragging == null)
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
        }
    }

    private async void Configure()
    {
        m_Root = UIManager.Instance.root;
        m_SlotGrid = m_Root.Q<VisualElement>($"Slot_{slotType}");

        await UniTask.WaitForEndOfFrame();

        // Remove only stale item visuals and old telegraph — preserve UXML background elements.
        for (int i = m_SlotGrid.childCount - 1; i >= 0; i--)
        {
            var child = m_SlotGrid[i];
            if (child is ItemVisual || child.name == "TelegraphEquip")
                child.RemoveFromHierarchy();
        }

        ConfigureTelegraph();

        GameServices.TryGet(out _audio);
        _equipmentHandler = playerController.Context.EquipmentHandler;
        EquippedStack = null;
        m_IsEquip = false;

        ContainerRegistry.Register((IItemContainer)this);
        ContainerRegistry.Register(this);
        NavigationRegistry.Register(this);

        RestoreFromEquipmentHandler();
    }

    // If EquipmentHandler already has an item equipped in this slot, create the ItemStack and spawn the visual.
    private void RestoreFromEquipmentHandler()
    {
        var itemData = _equipmentHandler.Get(slotType);
        if (itemData == null) return;

        // Never create a UI stack for the base weapon — it is an invisible fallback
        // for bare-hands combat and should not appear in any equipment slot.
        if (itemData is WeaponData wd)
        {
            var wh = playerController.GetComponent<WeaponHandler>();
            if (wh != null && wh.baseWeapon == wd) return;
        }

        // Reuse the existing stack from EquipmentHandler to preserve instanced data (rarity, modifiers).
        var existingStack = _equipmentHandler.GetStack(slotType);
        EquippedStack = existingStack ?? new ItemStack { data = itemData, quantity = 1, gridX = 0, gridY = 0 };
        m_IsEquip = true;
        TrySpawnEquippedVisual();
    }

    private void TrySpawnEquippedVisual()
    {
        var visual = new ItemVisual(EquippedStack, this);
        m_SlotGrid.Add(visual);

        visual.style.visibility = Visibility.Visible;
        visual.OriginContainer = this;
        EquippedStack.gridX = 0;
        EquippedStack.gridY = 0;

        visual.ApplyRotation(0, revertOnFail: false);
        EquippedStack.RootVisual = visual;

        ResizeSlotToItem(visual);
    }


    public async void AutoTransferItem(ItemStack itemStack)
    {
        if (TabViewManager.Instance.m_TabType != TabType.Inventory || PlayerInventory.Instance == null) return;

        var visual = itemStack.RootVisual;
        if (visual == null) return;

        RemoveStack(itemStack);
        m_SlotGrid.Remove(visual);
        visual.OriginContainer.HideTelegraph();

        ContainerRegistry.TransferVisualBetweenContainers(itemStack, PlayerInventory.Instance.GetInventoryGrid(),
            itemStack.gridX, itemStack.gridY);

        bool placed = await PlayerInventory.Instance.AutoPlaceItem(visual);

        if (!placed)
        {
            AddStack(itemStack);
            m_SlotGrid.Add(visual);
            visual.OriginContainer = this;
            visual.RestoreOriginal();
            return;
        }

        ContainerRegistry.TransferVisualBetweenContainers(itemStack,
            PlayerInventory.Instance.GetInventoryGrid(), itemStack.gridX, itemStack.gridY);

        PlayerInventory.Instance.AddStack(itemStack);
        visual.OriginContainer = PlayerInventory.Instance;
        visual.style.visibility = Visibility.Visible;
    }

    private void ConfigureTelegraph()
    {
        m_Telegraph = new VisualElement
        {
            name = "TelegraphEquip",
            style = { position = Position.Absolute, visibility = Visibility.Hidden }
        };

        m_Telegraph.AddToClassList("slot-icon-highlighted");
        m_SlotGrid.Add(m_Telegraph);
    }

    #region IItemContainer

    public ItemStack GetStackAt(int x, int y) => EquippedStack;
    public int GetGridWidth() => 1;
    public int GetGridHeight() => 1;
    public Rect GetCellRect(int x, int y) => m_SlotGrid.worldBound;
    public bool HasSpecialSlots() => true;
    public IReadOnlyList<Rect> GetSpecialSlotRects() => new List<Rect> { m_SlotGrid.worldBound };

    public (bool canPlace, Vector2 position) ShowPlacementTarget(ItemVisual draggedItem)
    {
        if (draggedItem == null || m_IsEquip && draggedItem.m_Stack != EquippedStack) return (false, Vector2.zero);

        if (draggedItem.m_Stack.data.itemType != Enums.ItemType.Equipable 
            || draggedItem.m_Stack.data is EquipableItemData equipData && equipData.equipSlot != slotType 
            || !draggedItem.worldBound.Overlaps(m_SlotGrid.worldBound))
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

    public void BeginDrag(ItemVisual dragged) => ContainerRegistry._currentlyDragging = dragged;

    public void EndDrag()
    {
        ContainerRegistry._currentlyDragging = null;
        m_Telegraph.style.visibility = Visibility.Hidden;

        if (EquippedStack == null) return;

        EquippedStack.RootVisual.ApplyRotation(0, revertOnFail: false);
        EquippedStack.gridX = 0;
        EquippedStack.gridY = 0;
    }

    public void AddStack(ItemStack stack)
    {
        stack.RootVisual.ApplyRotation(0, revertOnFail: false);
        stack.gridX = 0;
        stack.gridY = 0;
        EquippedStack = stack;
        m_IsEquip = true;

        _equipmentHandler.Equip(stack);
        OnEquipmentChanged?.Invoke();

        stack.RootVisual.OriginContainer = this;
        ResizeSlotToItem(stack.RootVisual);

        if (_uiSounds != null && _audio != null &&
            _uiSounds.TryGetEquipSound(slotType, out var sound) && !sound.IsNull)
        {
            _audio.PlayOneShot(sound, Vector3.zero);
        }
    }

    public void RemoveStack(ItemStack stack)
    {
        if (EquippedStack != stack) return;

        if (EquippedStack.data is EquipableItemData equipData)
        {
            _equipmentHandler.Unequip(equipData.equipSlot, false);
        }

        m_IsEquip = false;
        EquippedStack = null;
        ResizeSlotToDefault();
        OnEquipmentChanged?.Invoke();
    }

    public void RemoveItemStack(ItemStack stack)
    {
        RemoveStack(stack);
        stack.RootVisual?.RemoveFromHierarchy();
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        return m_SlotGrid.worldBound.Contains(worldPoint);
    }
    public ItemStack GetSameTypeOverlapItem(ItemVisual v) => null;
    public VisualElement GetInventoryGrid() => m_SlotGrid;
    public VisualElement GetTelegraph() => m_Telegraph;
    public ContainerType GetContainerType() => ContainerType.SlotEquip;
    public (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint) => (0, 0);
    public Task<bool> AutoPlaceItem(ItemVisual item) => throw new System.NotImplementedException();

    #endregion

    #region Helpers

    private void ResizeSlotToItem(ItemVisual item)
    {
        int w = item.GetSlotWidth();
        int h = item.GetSlotHeight();
        m_SlotGrid.style.width = PixelWidth(w);
        m_SlotGrid.style.height = PixelHeight(h);
        m_Telegraph.style.width = PixelWidth(w);
        m_Telegraph.style.height = PixelHeight(h);
    }

    private void ResizeSlotToDefault()
    {
        m_SlotGrid.style.width = PixelWidth(DefaultSlotSize.Width);
        m_SlotGrid.style.height = PixelHeight(DefaultSlotSize.Height);
        m_Telegraph.style.width = PixelWidth(DefaultSlotSize.Width);
        m_Telegraph.style.height = PixelHeight(DefaultSlotSize.Height);
    }

    #endregion
}