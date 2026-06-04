using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Static registry for all IItemContainer instances in the scene.
public class ContainerRegistry : MonoBehaviour
{
    #region Fields

    private static readonly List<IItemContainer> s_containers = new(16);
    private static readonly Dictionary<Enums.EquipSlot, EquipmentSlotContainer> s_equipSlots = new(8);
    private static readonly Dictionary<int, ConsumableSlotContainer> s_consumableSlots = new(4);

    public static ItemVisual _currentlyDragging = null;

    // Instance field set in Inspector — Awake bridges it into the static accessor.
    [SerializeField] private SlotConfigSO _slotConfigAsset;

    private static SlotConfigSO _slotConfig;
    private static Dimensions _measuredDimension;
    private static bool _hasMeasured;

    // Measured value (set by Loots/PlayerInventory from live layout) takes priority over SO default.
    public static Dimensions SlotDimension
    {
        get
        {
            if (_hasMeasured) return _measuredDimension;
            if (_slotConfig != null) return _slotConfig.SlotDimension;
            return new Dimensions { Width = 1, Height = 1 };
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_slotConfigAsset != null)
            _slotConfig = _slotConfigAsset;
    }

    private void Update()
    {
        var dragged = _currentlyDragging;
        if (dragged == null || !dragged.IsDragging) return;

        Rect cursorRect = InventoryCursorManager.Instance.GetCurrentCellRect();
        Vector2 worldCellPos = cursorRect.position + new Vector2(0.01f, 0.01f);

        var container = GetContainerAtPoint(worldCellPos);

        if (container != null)
        {
            if (container != dragged.HoverContainer)
            {
                dragged.HoverContainer?.HideTelegraph();

                (int gx, int gy) = container.WorldPointToGrid(worldCellPos);
                TransferVisualBetweenContainers(
                    dragged.m_Stack,
                    container.GetInventoryGrid(),
                    gx, gy);
            }

            var placement = container.ShowPlacementTarget(dragged);
            dragged.SetPlacementResults(placement);
            dragged.HoverContainer = container;
        }
        else
        {
            if (dragged.HoverContainer != null)
            {
                dragged.HoverContainer.GetTelegraph().style.visibility = Visibility.Hidden;
                dragged.HoverContainer = null;
            }
        }

        // Move the dragged visual to follow the cursor position every frame.
        Vector2 parentWorldPos = dragged.parent.worldBound.position;
        Vector2 localPos = new Vector2(
            worldCellPos.x - parentWorldPos.x + 20f,
            worldCellPos.y - parentWorldPos.y - 20f
        );

        dragged.SetPosition(localPos);
        dragged.MarkDirtyRepaint();
    }

    #endregion

    #region Public API

    /// Called by Loots/PlayerInventory after measuring the actual pixel size of a grid cell
    /// from the live UI layout. Overrides the SO default with real measured values.
    public static void SetMeasuredSlotDimension(Dimensions measured)
    {
        _measuredDimension = measured;
        _hasMeasured = true;
    }

    #endregion

    #region Container Registration

    public static void Register(IItemContainer container)
    {
        if (!s_containers.Contains(container))
            s_containers.Add(container);
    }

    public static void Unregister(IItemContainer container) => s_containers.Remove(container);

    public static void Register(EquipmentSlotContainer slot) => s_equipSlots[slot.slotType] = slot;
    public static void Unregister(EquipmentSlotContainer slot) => s_equipSlots.Remove(slot.slotType);

    public static EquipmentSlotContainer GetEquipmentSlot(Enums.EquipSlot slot)
    {
        s_equipSlots.TryGetValue(slot, out var container);
        return container;
    }

    public static void Register(ConsumableSlotContainer cs) => s_consumableSlots[cs.slotNumber] = cs;

    public static void Unregister(ConsumableSlotContainer cs)
    {
        if (s_consumableSlots.TryGetValue(cs.slotNumber, out var existing) && existing == cs)
            s_consumableSlots.Remove(cs.slotNumber);
    }

    public static ConsumableSlotContainer GetConsumableSlot(int slotNumber)
    {
        s_consumableSlots.TryGetValue(slotNumber, out var container);
        return container;
    }

    /// <summary>
    /// Returns the ConsumableSlotContainer that owns the given stack, or null if not found.
    /// Linear scan over at most 4 slots — cost is negligible for a one-shot use action.
    /// </summary>
    public static ConsumableSlotContainer GetConsumableSlotForStack(ItemStack stack)
    {
        if (stack == null) return null;

        foreach (var kvp in s_consumableSlots)
        {
            if (kvp.Value != null && kvp.Value.currentStack == stack)
                return kvp.Value;
        }

        return null;
    }

    #endregion

    #region Query

    public static IItemContainer GetContainerAtPoint(Vector2 worldPoint)
    {
        for (int i = s_containers.Count - 1; i >= 0; i--)
        {
            var c = s_containers[i];
            if (c != null && c.ContainsWorldPoint(worldPoint)) return c;
        }

        return null;
    }

    public static void TransferVisualBetweenContainers(
        ItemStack stack, VisualElement newGrid,
        int newGridX, int newGridY)
    {
        VisualElement visual = stack.RootVisual;
        if (visual == null || newGrid == null) return;

        Dimensions dim = SlotDimension;

        visual.style.position = Position.Absolute;

        Vector2 targetWorldPos = new Vector2(
            newGrid.worldBound.x + newGridX * dim.Width,
            newGrid.worldBound.y + newGridY * dim.Height);

        Vector2 localPos = new Vector2(
            targetWorldPos.x - newGrid.worldBound.x,
            targetWorldPos.y - newGrid.worldBound.y);


        newGrid.Add(visual);
        visual.style.left = localPos.x;
        visual.style.top = localPos.y;

        stack.RootVisual = (ItemVisual)visual;
        stack.gridX = newGridX;
        stack.gridY = newGridY;

        visual.BringToFront();
        visual.MarkDirtyRepaint();
    }

    public static void TransferStack(ItemStack stack, IItemContainer from, IItemContainer to)
    {
        
        if (from == to || to == null) return;

        from?.RemoveStack(stack);
        from?.HideTelegraph();
        
        to.AddStack(stack);
        stack.RootVisual.OriginContainer = to;
    }

    public static (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint)
    {
        Dimensions dim = SlotDimension;
        int gx = Mathf.FloorToInt(worldPoint.x / dim.Width);
        int gy = Mathf.FloorToInt(worldPoint.y / dim.Height);
        return (gx, gy);
    }

    #endregion
}