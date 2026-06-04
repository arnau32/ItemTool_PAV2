using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static UnityEngine.ProBuilder.AutoUnwrapSettings;

public sealed class PlayerInventory : MonoBehaviour, INavigatableContainer, ISaveable
{
    public enum LoadMode
    {
        UseSavedGridPosition,
        AutoPlaceSequentially
    }

    private LoadMode loadMode;
    private LootManager _lootManager;
    public static PlayerInventory Instance;

    private VisualElement progressBar_root_Inventory;
    private VisualElement progressBar_fill_Inventory;

    private VisualElement progressBar_root_Loot;
    private VisualElement progressBar_fill_Loot;

    public GameObject player;
    public bool IsActiveForNavigation() => TabViewManager.Instance.m_TabType == TabType.Inventory;
    public NavigationGroup NavGroup => NavigationGroup.Inventory;
    public int NavOrder => 0;

    [SerializeField] private int _auroraDust = 0; // Currency

    public event System.Action OnAuraDustChanged;

    private SaveService _save;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (GameServices.TryGet<SaveService>(out _save))
            _save.RegisterSaveable(this);
    }

    private void OnDisable()
    {
        if (_save == null) return;
        if (_save.IsDeathPenaltyApplied) return;
        CaptureToSave(_save.CurrentSave);
    }

    private void OnDestroy()
    {
        _save?.UnregisterSaveable(this);
    }

    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;

    private float holdTime = 0f;
    private float delayTime = 0.2f;
    private float holdDuration = 0.4f;

    private bool isHolding = false;
    private bool _holdTriggered = false;

    [SerializeField] private float _holdConsumeDuration  = 0.8f;
    [SerializeField] private float _baseLootDelay        = 0.3f;
    [SerializeField] private float _extraDimensionDelay  = 0.1f;

    private bool            _isConsumeHolding;
    private float           _consumeHoldTime;
    private IItemContainer  _consumeHoldContainer;
    private ItemStack       _consumeHoldStack;
    private bool            _isXButtonHeld;

    private bool                    _isSequentialLooting;
    private CancellationTokenSource _sequentialLootCts;

    private void Update()
    {
        if (m_Telegraph == null) return;

        if (ContainerRegistry._currentlyDragging == null)
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
        }

        TickConsumeHold();

        if (!isHolding) return;

        if (!loots.isActive)
        {
            return;
        }

        holdTime += Time.deltaTime;

        // ❗前摇阶段（不显示）
        if (holdTime < delayTime)
        {
            return;
        }

        // ❗开始显示进度条
        var root = GetCurrentRoot();
        if (root != null && root.style.display != DisplayStyle.Flex)
        {
            root.style.display = DisplayStyle.Flex;
        }

        float t = Mathf.Clamp01((holdTime - delayTime) / holdDuration);

        var fill = GetCurrentFill();
        if (fill != null)
        {
            fill.style.width = Length.Percent(t * 100);
        }

        if (t >= 1f && !_holdTriggered)
        {
            _holdTriggered = true;
            isHolding = false;
            progressBar_root_Inventory.style.display = DisplayStyle.None;
            progressBar_root_Loot.style.display = DisplayStyle.None;
            if (_uiSounds != null) PlayUISound(_uiSounds.LootAllComplete);
            _ = ExecuteTransfer();
        }
    }

    public Loots loots;

    // UI references
    private VisualElement m_Root;
    private VisualElement m_InventoryGrid;
    private Vector2 _cachedGridOrigin;

    public VisualElement GetInventoryGrid()
    {
        return m_InventoryGrid;
    }

    private VisualElement m_Telegraph;

    public VisualElement GetTelegraph()
    {
        return m_Telegraph;
    }

    public int AuraDust => _auroraDust;

    public bool TrySpend(int amount)
    {
        if (_auroraDust < amount) return false;
        _auroraDust -= amount;
        OnAuraDustChanged?.Invoke();
        return true;
    }

    public void AddAuraDust(int amount)
    {
        _auroraDust += amount;
        OnAuraDustChanged?.Invoke();
    }

    public int GetGridWidth() => InventoryDimensions.Width;
    public int GetGridHeight() => InventoryDimensions.Height;

    public Rect GetCellRect(int x, int y)
    {
        // worldBound is (0,0,0,0) when the inventory panel is display:none.
        // Fall back to the cached origin (updated whenever the panel is visible).
        float gx = m_InventoryGrid.worldBound.width > 0
            ? m_InventoryGrid.worldBound.x
            : _cachedGridOrigin.x;
        float gy = m_InventoryGrid.worldBound.width > 0
            ? m_InventoryGrid.worldBound.y
            : _cachedGridOrigin.y;

        return new Rect(gx + x * SlotDimension.Width, gy + y * SlotDimension.Height, SlotDimension.Width, SlotDimension.Height);
    }

    /// <summary>
    /// Stores the current grid world origin for use when the panel is hidden.
    /// Call this after any operation that changes the grid size and after UIToolkit
    /// has had at least one frame to recompute the layout.
    /// </summary>
    public void CacheGridOrigin()
    {
        if (m_InventoryGrid.worldBound.width > 0)
            _cachedGridOrigin = new Vector2(m_InventoryGrid.worldBound.x, m_InventoryGrid.worldBound.y);
    }

    public bool HasSpecialSlots() => false;

    public IReadOnlyList<Rect> GetSpecialSlotRects() => null;

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

    // Slot pixel size
    public static Dimensions SlotDimension { get; private set; }

    // Inventory & layout data
    public List<ItemStack> ItemStacks = new List<ItemStack>();

    public event System.Action<ItemStack> OnItemAdded;

    public Dimensions InventoryDimensions;

    private bool m_IsInventoryReady;
    public bool IsInventoryReady => m_IsInventoryReady;
    public bool InventoryInit;

    public bool CanOpenInventory;

    public bool IsOpen =>
        UIManager.Instance != null &&
        UIManager.Instance.tabViewPanel != null &&
        UIManager.Instance.tabViewPanel.resolvedStyle.display != DisplayStyle.None;


    private void Start()
    {
        GameServices.TryGet(out _lootManager);
        GameServices.TryGet(out _audio);
        Configure();
        LoadItems();
    }

    private float _moveNextTime = 0f;
    private Vector2 _lastMoveDir = Vector2.zero;

    public void OnMoveCursor(InputAction.CallbackContext ctx)
    {
        Vector2 dir = ctx.ReadValue<Vector2>();

        if (dir.magnitude < 0.3f)
        {
            _lastMoveDir = Vector2.zero;
            return;
        }

        dir = dir.normalized;

        if (_lastMoveDir == Vector2.zero)
        {
            if (!TryMoveCursorDirectional(dir)) return;

            if (_uiSounds != null) PlayUISound(_uiSounds.CursorMove);
            _lastMoveDir = dir;
            _moveNextTime = Time.time + InventoryCursorSensitivity.Instance.moveRepeatDelay;
        }
        else
        {
            if (Time.time >= _moveNextTime)
            {
                if (TryMoveCursorDirectional(dir))
                {
                    if (_uiSounds != null) PlayUISound(_uiSounds.CursorMove);
                    _moveNextTime = Time.time + InventoryCursorSensitivity.Instance.moveRepeatRate;
                }
            }
        }
    }

    // D-pad navigates the inventory cursor instead of quick-equipping.
    public void OnDpadQuickEquip(InputAction.CallbackContext ctx)
    {
        OnMoveCursor(ctx);
    }

    public void OnDpadConsumeHoldStarted(InputAction.CallbackContext ctx)
    {
        OnMoveCursor(ctx);
    }

    public void OnDpadConsumeHoldCanceled(InputAction.CallbackContext ctx)
    {
        _lastMoveDir = Vector2.zero;
    }

    private void QuickEquipConsumable(int slotNumber, ItemStack sourceStack)
    {
        var slot = ContainerRegistry.GetConsumableSlot(slotNumber);
        if (slot == null) return;

        var targetStack = slot.currentStack;

        // ===== 情况 1：槽为空 =====
        if (targetStack == null)
        {
            MoveStackToSlot(sourceStack, slot);
            return;
        }

        // ===== 情况 2：同类物品 → 叠加 =====
        bool sameType =
            targetStack.data == sourceStack.data ||
            targetStack.data.uniqueID == sourceStack.data.uniqueID;

        if (sameType)
        {
            int canAdd = targetStack.data.maxStack - targetStack.quantity;
            int toAdd  = Mathf.Min(canAdd, sourceStack.quantity);

            if (toAdd > 0)
            {
                targetStack.quantity += toAdd;
                targetStack.RootVisual.UpdateCountLabel();
                sourceStack.quantity -= toAdd;
            }

            if (sourceStack.quantity <= 0)
            {
                sourceStack.RootVisual.OriginContainer.RemoveItemStack(sourceStack);
                return;
            }

            // Overflow: try a free consumable slot, else leave in inventory
            sourceStack.RootVisual.UpdateCountLabel();
            TryMoveToFreeConsumableSlot(sourceStack);
            return;
        }

        // ===== 情况 3：不同物品 → 替换 =====
        SwapStacks(sourceStack, targetStack, slot);
    }

    private void TryMoveToFreeConsumableSlot(ItemStack stack)
    {
        for (int i = 0; i < 4; i++)
        {
            var slot = ContainerRegistry.GetConsumableSlot(i);
            if (slot == null || slot.currentStack != null) continue;
            MoveStackToSlot(stack, slot);
            return;
        }
        // No free slot — overflow stays in inventory
    }

    private async void SwapStacks(ItemStack sourceStack, ItemStack targetStack, ConsumableSlotContainer slot)
    {
        var sourceOrigin = sourceStack.RootVisual.OriginContainer;
        var targetVisual = targetStack.RootVisual;

        // ① 移除目标槽物品
        slot.RemoveItemStack(targetStack);
        //slot.GetInventoryGrid().Remove(targetVisual);

        // ② 把 source 放进 slot
        MoveStackToSlot(sourceStack, slot);

        // ③ 尝试把旧 target 自动放回原容器
        ContainerRegistry.TransferVisualBetweenContainers(
            targetStack,
            sourceOrigin.GetInventoryGrid(),
            0,
            0
        );

        bool placed = await sourceOrigin.AutoPlaceItem(targetVisual);

        if (!placed)
        {
            Debug.LogWarning("背包没有空间放回被替换的物品");

            // fallback：放回 slot（回滚）
            slot.RemoveItemStack(sourceStack);
            slot.GetInventoryGrid().Remove(sourceStack.RootVisual);

            MoveStackToSlot(targetStack, slot);
            return;
        }

        sourceOrigin.AddStack(targetStack);
        targetVisual.OriginContainer = sourceOrigin;
    }

    private void MoveStackToSlot(ItemStack stack, ConsumableSlotContainer slot)
    {
        var visual = stack.RootVisual;
        var container = visual.OriginContainer;

        visual.OriginContainer.RemoveStack(stack);
        visual.OriginContainer.GetInventoryGrid().Remove(visual);

        stack.gridX = 0;
        stack.gridY = 0;
        slot.GetInventoryGrid().Add(visual);
        visual.ApplyRotation(0, revertOnFail: false);
        visual.style.left   = 0;
        visual.style.top    = 0;
        visual.style.width  = 75;
        visual.style.height = 75;

        slot.AddStack(stack);
        container.GetTelegraph().style.visibility = Visibility.Hidden;

        visual.OriginContainer = slot;
        visual.style.visibility = Visibility.Visible;
    }

    private int GetSlotFromDpad(Vector2 input)
    {
        if (input == Vector2.up) return 0;
        if (input == Vector2.left) return 1;
        if (input == Vector2.right) return 2;
        if (input == Vector2.down) return 3;

        return -1;
    }

    private static bool IsNavNodeCompatible(NavigationNode node, ItemStack cursorItem)
    {
        if (ContainerRegistry._currentlyDragging == null) return true;

        if (cursorItem?.data == null) return true;

        var target = node.container as IItemContainer;
        if (target == null) return true;

        var containerType = target.GetContainerType();
        if (containerType == ContainerType.Grids) return true;

        if (containerType == ContainerType.SlotConsum)
            return cursorItem.data is ConsumableItemData;

        if (containerType == ContainerType.SlotEquip)
        {
            if (cursorItem.data is not EquipableItemData equipData) return false;
            if (node.container is not EquipmentSlotContainer equipSlot) return false;
            return equipSlot.slotType == equipData.equipSlot;
        }

        return true;
    }

    public bool TryMoveCursorDirectional(Vector2 dir)
    {
        if (InventoryCursorManager.Instance.currentContainer == null) return false;

        dir.y = -dir.y;

        Vector2 dirN = dir.normalized;
        if (dirN.sqrMagnitude < 0.001f) return false;

        var navC = InventoryCursorManager.Instance.currentContainer as INavigatableContainer;
        if (navC != null)
        {
            int gx = InventoryCursorManager.Instance.gridX;
            int gy = InventoryCursorManager.Instance.gridY;

            int stepX = (Mathf.Abs(dirN.x) > 0.5f) ? (dirN.x > 0 ? 1 : -1) : 0;
            int stepY = (Mathf.Abs(dirN.y) > 0.5f) ? (dirN.y > 0 ? 1 : -1) : 0;

            int newX = gx;
            int newY = gy;

            var stack = navC.GetStackAt(gx, gy);
            bool isDragging = ContainerRegistry._currentlyDragging != null;

            // =========================
            // Grid 内移动逻辑
            // =========================
            if (stack != null && !isDragging)
            {
                gx = stack.gridX;
                gy = stack.gridY;

                if (stack.RootVisual != null)
                {
                    int w = stack.RootVisual.GetSlotWidth();
                    int h = stack.RootVisual.GetSlotHeight();

                    if (stepX > 0) newX = stack.gridX + w;
                    if (stepX < 0) newX = stack.gridX - 1;

                    if (stepY > 0) newY = stack.gridY + h;
                    if (stepY < 0) newY = stack.gridY - 1;
                }
                else
                {
                    newX = gx + stepX;
                    newY = gy + stepY;
                }
            }
            else
            {
                newX = gx + stepX;
                newY = gy + stepY;
            }

            bool forceOutOfBounds = false;
            if (isDragging && ContainerRegistry._currentlyDragging != null)
            {
                int dw = ContainerRegistry._currentlyDragging.GetSlotWidth();
                int dh = ContainerRegistry._currentlyDragging.GetSlotHeight();
                int clampedX = Mathf.Clamp(newX, 0, navC.GetGridWidth()  - dw);
                int clampedY = Mathf.Clamp(newY, 0, navC.GetGridHeight() - dh);
                // If clamp changed the target, we're already at the size boundary —
                // force cross-container navigation instead of staying put.
                if (clampedX != newX || clampedY != newY)
                    forceOutOfBounds = true;
                else
                {
                    newX = clampedX;
                    newY = clampedY;
                }
            }

            bool inBounds = !forceOutOfBounds &&
                newX >= 0 && newX < navC.GetGridWidth() &&
                newY >= 0 && newY < navC.GetGridHeight();

            // =========================
            // 成功在当前 grid 内移动
            // =========================
            if (inBounds && (stepX != 0 || stepY != 0))
            {
                InventoryCursorManager.Instance.gridX = newX;
                InventoryCursorManager.Instance.gridY = newY;

                InventoryCursorManager.Instance.MoveTo(navC.GetCellRect(newX, newY));

                var stackAtTarget = navC.GetStackAt(newX, newY);
                InventoryCursorManager.Instance.UpdateSizeFromItem(stackAtTarget);
                return true;
            }

            // =========================
            // ❗关键修复：越界 → 用"目标位置"参与跨容器计算
            // =========================
            int queryX = newX;
            int queryY = newY;

            // clamp 防止 GetCellRect 崩
            queryX = Mathf.Clamp(queryX, 0, navC.GetGridWidth() - 1);
            queryY = Mathf.Clamp(queryY, 0, navC.GetGridHeight() - 1);

            Vector2 curCenter = navC.GetInventoryGrid().worldBound.position
                + navC.GetCellRect(queryX, queryY).center;

            float minDot = 0.35f;
            NavigationNode best = null;
            float bestScore = float.MinValue;
            float bestAlongDistance = float.MaxValue;
            float bestDistance = float.MaxValue;

            foreach (var node in NavigationRegistry.Nodes)
            {
                Vector2 delta = node.worldCenter - curCenter;
                //delta.y = -delta.y;

                float dist = delta.magnitude;
                if (dist < 1e-3f) continue;

                Vector2 deltaN = delta / dist;
                float dot = Vector2.Dot(deltaN, dirN);
                if (dot < minDot) continue;

                if (!IsNavNodeCompatible(node, isDragging ? ContainerRegistry._currentlyDragging?.m_Stack : stack)) continue;

                float along = Vector2.Dot(delta, dirN);

                float score = dot - (along * 0.001f) - (dist * 0.0001f);

                if (score > bestScore ||
                    (Mathf.Approximately(score, bestScore) && along < bestAlongDistance) ||
                    (Mathf.Approximately(score, bestScore) &&
                     Mathf.Approximately(along, bestAlongDistance) &&
                     dist < bestDistance))
                {
                    best = node;
                    bestScore = score;
                    bestAlongDistance = along;
                    bestDistance = dist;
                }
            }

            if (best != null)
            {
                LandOnNode(best, isDragging);
                return true;
            }

            // =========================
            // fallback：最近节点
            // =========================
            NavigationNode nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var node in NavigationRegistry.Nodes)
            {
                if (!IsNavNodeCompatible(node, isDragging ? ContainerRegistry._currentlyDragging?.m_Stack : stack)) continue;

                float d = Vector2.Distance(node.center, curCenter);
                if (!(d < nearestDist) || !(d > 1e-3f)) continue;

                nearestDist = d;
                nearest = node;
            }

            if (nearest != null)
            {
                LandOnNode(nearest, isDragging);
                return true;
            }
        }

        return false;
    }

    private void LandOnNode(NavigationNode node, bool isDragging)
    {
        int landX = node.gridX;
        int landY = node.gridY;

        if (isDragging && ContainerRegistry._currentlyDragging != null)
        {
            var targetNav = node.container as INavigatableContainer;
            if (targetNav != null)
            {
                int dw = ContainerRegistry._currentlyDragging.GetSlotWidth();
                int dh = ContainerRegistry._currentlyDragging.GetSlotHeight();
                landX = Mathf.Clamp(landX, 0, Mathf.Max(0, targetNav.GetGridWidth()  - dw));
                landY = Mathf.Clamp(landY, 0, Mathf.Max(0, targetNav.GetGridHeight() - dh));
            }
        }

        InventoryCursorManager.Instance.currentContainer = node.container;
        InventoryCursorManager.Instance.gridX = landX;
        InventoryCursorManager.Instance.gridY = landY;
        InventoryCursorManager.Instance.MoveTo(node.container.GetCellRect(landX, landY));
    }

    public void OnSwitchContainerLeft(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        SwitchCursorContainer(-1);
    }

    public void OnSwitchContainerRight(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        SwitchCursorContainer(+1);
    }

    public void SwitchCursorContainer(int direction)
    {
        var list = NavigationRegistry.OrderedContainers;
        if (list.Count == 0) return;

        var current      = InventoryCursorManager.Instance.currentContainer;
        var currentGroup = (current as INavigatableContainer)?.NavGroup ?? NavigationGroup.Inventory;
        var dragging     = ContainerRegistry._currentlyDragging?.m_Stack;

        // Build ordered list of active sections (unique NavGroups).
        var sections = new List<NavigationGroup>();
        foreach (var c in list)
        {
            if (!c.IsActiveForNavigation()) continue;
            if (!sections.Contains(c.NavGroup)) sections.Add(c.NavGroup);
        }
        if (sections.Count == 0) return;
        sections.Sort();

        int curIdx  = sections.IndexOf(currentGroup);
        if (curIdx < 0) curIdx = 0;

        // Step through sections until we find one with a compatible container.
        for (int step = 1; step <= sections.Count; step++)
        {
            int nextIdx   = (curIdx + direction * step + sections.Count) % sections.Count;
            var nextGroup = sections[nextIdx];

            foreach (var container in list)
            {
                if (!container.IsActiveForNavigation()) continue;
                if (container.NavGroup != nextGroup) continue;

                if (dragging != null)
                {
                    var ct = (container as IItemContainer)?.GetContainerType();
                    if (ct == ContainerType.SlotConsum && dragging.data is not ConsumableItemData) continue;
                    if (ct == ContainerType.SlotEquip)
                    {
                        if (dragging.data is not EquipableItemData ed) continue;
                        if (container is not EquipmentSlotContainer es) continue;
                        if (es.slotType != ed.equipSlot) continue;
                    }
                }

                MoveCursorToContainerFirstCell(container);
                return;
            }
        }
    }

    private void MoveCursorToContainerFirstCell(INavigatableContainer container)
    {
        // 优先普通 grid
        if (container.GetGridWidth() > 0 && container.GetGridHeight() > 0)
        {
            InventoryCursorManager.Instance.currentContainer = container;
            InventoryCursorManager.Instance.gridX = 0;
            InventoryCursorManager.Instance.gridY = 0;
            InventoryCursorManager.Instance.MoveTo(container.GetCellRect(0, 0));
            return;
        }

        // 如果没有 grid，用特殊槽位
        if (container.HasSpecialSlots())
        {
            var slots = container.GetSpecialSlotRects();
            if (slots != null && slots.Count > 0)
            {
                InventoryCursorManager.Instance.currentContainer = container;
                InventoryCursorManager.Instance.gridX = -1;
                InventoryCursorManager.Instance.gridY = 0;
                InventoryCursorManager.Instance.MoveTo(slots[0]);
            }
        }
    }

    /// Initialize UI references, wait layout, measure slot, setup telegraph
    private async void Configure()
    {
        m_Root = UIManager.Instance.tabViewPanel;
        m_Root.focusable = true;

        m_InventoryGrid = m_Root.Q<VisualElement>("Grid");
        loadMode = LoadMode.AutoPlaceSequentially;

        progressBar_root_Inventory = m_Root.Q<VisualElement>("progress-root_Inventory");
        progressBar_fill_Inventory = m_Root.Q<VisualElement>("progress-fill_Inventory");

        progressBar_root_Loot = m_Root.Q<VisualElement>("progress-root_Loot");
        progressBar_fill_Loot = m_Root.Q<VisualElement>("progress-fill_Loot");

        await UniTask.WaitUntil(() =>
            m_InventoryGrid.worldBound.width > 0
        );

        // Ensure ApplyFromSave has run before creating the grid so the capacity
        // level is correct (avoids building a level-0 grid if save applies late).
        if (GameServices.TryGet<SaveService>(out var saveForCap))
            await UniTask.WaitUntil(() => saveForCap.IsSaveApplied);

        progressBar_root_Inventory.style.display = DisplayStyle.None;
        progressBar_root_Loot.style.display = DisplayStyle.None;

        m_InventoryGrid.Clear();
        PlayerInventoryCapacityController.Instance.InitInventoryGridCapacity();

        ConfigureSlotDimensions();
        ConfigureInventoryTelegraph();

        // Wait for UIToolkit to recompute worldBound after the grid was rebuilt,
        // then cache the origin so GetCellRect works while the panel is hidden.
        await UniTask.WaitUntil(() => m_InventoryGrid.worldBound.width > 0);
        CacheGridOrigin();

        m_IsInventoryReady = true;
        CanOpenInventory = true;

        InitCursor();

        ContainerRegistry.Register(this);
        NavigationRegistry.Register(this);
    }

    public void InitInventory()
    {
        //InitCursor();
        InventoryCursorManager.Instance.m_Cursor.style.display = DisplayStyle.Flex;

        GameServices.Get<InputService>().OnUIInventoryOpen();

        ResetCursorToInventoryStart();
        var stack = GetStackAt(0, 0);
        InventoryCursorManager.Instance.UpdateSizeFromItem(stack);
    }

    public void CloseInventory()
    {
        if (ContainerRegistry._currentlyDragging != null)
        {
            ContainerRegistry._currentlyDragging.HoverContainer.HideTelegraph();
            ContainerRegistry._currentlyDragging.OriginContainer.HideTelegraph();
            ContainerRegistry._currentlyDragging.RestoreOriginal();
        }

        loots.CloseLoots();

        InventoryCursorManager.Instance.ShowBorder();
    }

    private void InitCursor()
    {
        var cursor = UIManager.Instance.tabViewPanel.Q<VisualElement>("InventoryCursor");

        if (cursor == null)
        {
            Debug.LogError("InventoryCursor not found in UXML.");
            return;
        }
        InventoryCursorManager.Instance.m_Cursor = cursor;
    }

    public void ResetCursorToInventoryStart()
    {
        InventoryCursorManager.Instance.currentContainer = this;
        InventoryCursorManager.Instance.gridX = 0;
        InventoryCursorManager.Instance.gridY = 0;
        InventoryCursorManager.Instance.MoveTo(GetCellRect(0, 0));
    }

    private void ResetCursorToLootStart()
    {
        InventoryCursorManager.Instance.currentContainer = loots;
        InventoryCursorManager.Instance.gridX = 0;
        InventoryCursorManager.Instance.gridY = 0;
        InventoryCursorManager.Instance.MoveTo(loots.GetCellRect(0, 0));
        InventoryCursorManager.Instance.m_Cursor.style.left = 666.75f;
        InventoryCursorManager.Instance.m_Cursor.style.top = 170;
    }

    public void OnPickUpItem(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        PlayerInventory.Instance.OnCursorConfirm();
    }

    public void OnAutoTransferItem(InputAction.CallbackContext ctx)
    {
        if (!ctx.canceled) return;
        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, loots)) return;

        if (ContainerRegistry._currentlyDragging == null)
        {
            // try pick item
            TryTransferItem();
        }

        InventoryCursorManager.Instance.UpdateInfo();
    }


    /// <summary>
    /// ///////////////////////////////////////////////////////////////////
    /// 一键全传
    /// </summary>
    /// <returns></returns>
    ///
    private bool isTransferring = false;

    public async Task ExecuteTransfer()
    {
        if (isTransferring) return;
        if (ContainerRegistry._currentlyDragging != null) return;

        isTransferring = true;

        try
        {
            if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, this) && loots.isActive)
            {
                await SmartTransferAllToLoots();
            }
            else if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, loots))
            {
                await SmartTransferAllToInventory();
            }
        }
        finally
        {
            isTransferring = false;
        }
    }

    public async Task SmartTransferAllToInventory()
    {
        // ① 复制数据（不要直接用原对象！）
        var originalStacks = loots.ItemStacks.ToList()
                                 .Select(s => s.Clone())
                                 .ToList();

        // ② 清空来源容器
        for (int i = loots.ItemStacks.Count - 1; i >= 0; i--)
        {
            var stack = loots.ItemStacks[i];

            if (stack?.RootVisual != null)
            {
                stack.RootVisual.RemoveFromHierarchy();
                stack.RootVisual.CleanupRotatedAssets();
            }
        }

        loots.ItemStacks.Clear();

        // ③ 记录失败项
        List<ItemStack> failed = new();

        foreach (var stack in originalStacks)
        {
            bool success = await TryTransferStackToInventory(stack);

            if (!success)
            {
                failed.Add(stack);
            }
        }

        // ④ 回填失败项（AutoPlace）
        foreach (var stack in failed)
        {
            bool success = await TryTransferStackToLoot(stack);

            if (!success)
            {
                Debug.LogWarning($"Item lost: {stack.data.itemNameID}");
            }
        }
    }

    public async Task SmartTransferAllToLoots()
    {
        // ① 复制数据（不要直接用原对象！）
        var originalStacks = ItemStacks.ToList()
                                 .Select(s => s.Clone())
                                 .ToList();

        // ② 清空来源容器
        for (int i = ItemStacks.Count - 1; i >= 0; i--)
        {
            var stack = ItemStacks[i];

            if (stack?.RootVisual != null)
            {
                stack.RootVisual.RemoveFromHierarchy();
                stack.RootVisual.CleanupRotatedAssets();
            }
        }

        ItemStacks.Clear();

        // ③ 记录失败项
        List<ItemStack> failed = new();

        foreach (var stack in originalStacks)
        {
            bool success = await TryTransferStackToLoot(stack);

            if (!success)
            {
                failed.Add(stack);
            }
        }

        // ④ 回填失败项（AutoPlace）
        foreach (var stack in failed)
        {
            bool success = await TryTransferStackToInventory(stack);

            if (!success)
            {
                Debug.LogWarning($"Item lost: {stack.data.itemNameID}");
            }
        }
    }

    private async Task<bool> TryTransferStackToLoot(ItemStack stack)
    {
        stack.RootVisual.SetOriginalRotateIndex(stack.rotationIndex);
        var visual = new ItemVisual(stack, loots);
        stack.RootVisual = visual;

        loots.GetInventoryGrid().Add(visual);

        // 👉 先尝试堆叠
        var stackTarget = loots.FindStackForAutoStack(stack);

        int remaining = stack.quantity;

        if (stackTarget != null)
        {
            int canAdd = stackTarget.data.maxStack - stackTarget.quantity;
            int toMove = Mathf.Min(canAdd, remaining);

            stackTarget.quantity += toMove;
            remaining -= toMove;
        }

        if (remaining <= 0)
        {
            return true;
        }

        // 👉 再尝试放置
        bool placed = await loots.AutoPlaceItem(visual);

        if (!placed)
        {
            visual.RemoveFromHierarchy();
            return false;
        }

        loots.AddStack(stack);
        visual.style.visibility = Visibility.Visible;

        return true;
    }

    private async Task<bool> TryTransferStackToInventory(ItemStack stack)
    {
        stack.RootVisual.SetOriginalRotateIndex(stack.rotationIndex);
        var visual = new ItemVisual(stack, this);
        stack.RootVisual = visual;

        int originalQuantity = stack.quantity;

        if (stack.data is ConsumableItemData)
        {
            for (int i = 0; i < 4 && stack.quantity > 0; i++)
            {
                var dpad = ContainerRegistry.GetConsumableSlot(i);
                if (dpad?.currentStack?.data == null) continue;

                bool sameType = dpad.currentStack.data == stack.data ||
                                (!string.IsNullOrEmpty(dpad.currentStack.data.uniqueID) &&
                                 dpad.currentStack.data.uniqueID == stack.data.uniqueID);
                if (!sameType) continue;

                int canAdd = dpad.currentStack.data.maxStack - dpad.currentStack.quantity;
                if (canAdd <= 0) continue;

                int toAdd = Mathf.Min(canAdd, stack.quantity);
                dpad.currentStack.quantity += toAdd;
                dpad.currentStack.RootVisual?.UpdateCountLabel();
                stack.quantity -= toAdd;
            }

            if (stack.quantity <= 0)
            {
                visual.RemoveFromHierarchy();
                visual.CleanupRotatedAssets();
                TrackLootPickedUp(stack, originalQuantity);
                return true;
            }

            for (int i = 0; i < 4; i++)
            {
                var dpad = ContainerRegistry.GetConsumableSlot(i);
                if (dpad?.m_SlotGrid == null || dpad.currentStack != null) continue;

                stack.gridX = 0;
                stack.gridY = 0;
                dpad.m_SlotGrid.Add(visual);
                visual.ApplyRotation(0, revertOnFail: false);
                visual.style.left   = 0;
                visual.style.top    = 0;
                visual.style.width  = 75;
                visual.style.height = 75;
                visual.style.visibility = Visibility.Visible;
                dpad.AddStack(stack);
                TrackLootPickedUp(stack, originalQuantity);
                return true;
            }
        }

        GetInventoryGrid().Add(visual);

        var stackTarget = FindStackForAutoStack(stack);
        int remaining = stack.quantity;

        if (stackTarget != null)
        {
            int canAdd = stackTarget.data.maxStack - stackTarget.quantity;
            int toMove = Mathf.Min(canAdd, remaining);

            stackTarget.quantity += toMove;
            remaining -= toMove;
            stack.quantity = remaining;

            stackTarget.RootVisual.UpdateCountLabel();
            NotifyItemArrived(stackTarget.data, toMove);
        }

        if (remaining <= 0)
        {
            visual.RemoveFromHierarchy();
            visual.CleanupRotatedAssets();
            TrackLootPickedUp(stack, originalQuantity);
            return true;
        }

        visual.UpdateCountLabel();

        bool placed = await AutoPlaceItem(visual);

        if (!placed)
        {
            visual.RemoveFromHierarchy();
            return false;
        }

        AddStack(stack);
        visual.style.visibility = Visibility.Visible;
        TrackLootPickedUp(stack, originalQuantity);
        return true;
    }

    private void TrackLootPickedUp(ItemStack stack, int quantity)
    {
        if (stack?.data == null || quantity <= 0) return;
        Vector3 pos = player != null ? player.transform.position : Vector3.zero;
        string id = !string.IsNullOrEmpty(stack.data.uniqueID) ? stack.data.uniqueID : stack.data.itemNameID;
        CombatAnalytics.LootPickedUp(id, stack.data.itemType.ToString(), stack.data.value, quantity, pos);
    }
    /// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>

    private void TryTransferItem()
    {
        var container = InventoryCursorManager.Instance.currentContainer;
        if (container == null) return;

        var stack = container.GetStackAt(InventoryCursorManager.Instance.gridX, InventoryCursorManager.Instance.gridY);
        if (stack == null) return;

        switch (container.GetContainerType())
        {
            case ContainerType.Grids:
                // Inventory <-> Loot 互转
                stack.RootVisual.OriginContainer.AutoTransferItem(stack);
                break;

            case ContainerType.SlotEquip:
                AutoTransferFromSlotToLoot(container, stack);
                break;

            case ContainerType.SlotConsum:
                // Loot abierto → devuelve al loot. Sin loot → devuelve al inventario.
                if (loots != null && loots.isActive)
                    AutoTransferFromSlotToLoot(container, stack);
                else
                    stack.RootVisual.OriginContainer.AutoTransferItem(stack);
                break;
        }
    }

    private async void AutoTransferFromSlotToLoot(IItemContainer slotContainer, ItemStack stack)
    {
        if (loots == null || !loots.isActive)
            return;

        var visual = stack.RootVisual;
        if (visual == null) return;

        // ① 从原槽移除
        slotContainer.RemoveItemStack(stack);

        visual.OriginContainer.HideTelegraph();

        // ② 尝试堆叠到 loots
        ItemStack stackTarget = loots.FindStackForAutoStack(stack);

        if (stackTarget != null)
        {
            int canAdd = stackTarget.data.maxStack - stackTarget.quantity;
            int toMove = Mathf.Min(canAdd, stack.quantity);

            stackTarget.quantity += toMove;
            stack.quantity -= toMove;
            stackTarget.RootVisual.UpdateCountLabel();

            if (stack.quantity <= 0)
            {
                visual.RemoveFromHierarchy();
                visual.CleanupRotatedAssets();
                return;
            }

            visual.UpdateCountLabel();
        }

        // ③ 放入 loots 空位
        ContainerRegistry.TransferVisualBetweenContainers(
            stack,
            loots.GetInventoryGrid(),
            0, 0
        );

        bool placed = await loots.AutoPlaceItem(visual);
        if (!placed)
        {
            // 放不下就还原（理论上不常见）
            visual.RestoreOriginal();
            return;
        }

        loots.AddStack(stack);
        visual.OriginContainer = loots;
        visual.style.visibility = Visibility.Visible;
    }

    public async void AutoTransferItem(ItemStack itemStack)
    {
        if (loots == null) return;

        var visual = itemStack.RootVisual;
        if (visual == null) return;

        visual.SetOriginalRotateIndex(visual._rotationIndex);
        if (!loots.isActive)
        {
            if (!loots.CanFitItem(visual))
            {
                loots.DropAllToWorld();

                if (!loots.CanFitItem(visual))
                {
                    DropItemStackToWorld(itemStack);
                    return;
                }
            }
        }

        RemoveStack(itemStack);
        RemoveItemFromInventoryGrid(visual);

        visual.OriginContainer.HideTelegraph();

        ItemStack stackTarget = loots.FindStackForAutoStack(itemStack);

        if (stackTarget != null)
        {
            int canAdd = stackTarget.data.maxStack - stackTarget.quantity;
            int toMove = Mathf.Min(canAdd, itemStack.quantity);

            stackTarget.quantity += toMove;
            itemStack.quantity -= toMove;

            stackTarget.RootVisual.UpdateCountLabel();

            if (itemStack.quantity <= 0)
            {
                visual.RemoveFromHierarchy();
                visual.CleanupRotatedAssets();
                return;
            }

            visual.UpdateCountLabel();
        }

        visual.SetOriginalPosition(visual.worldBound.position);

        var oldGrid = visual.parent;
        if (oldGrid != null) oldGrid.Remove(visual);
        loots.GetInventoryGrid().Add(visual);

        bool placedSuccessfully = await loots.AutoPlaceItem(visual);

        if (!placedSuccessfully)
        {
            // Restore to loot grid.
            loots.GetInventoryGrid().Remove(visual);
            this.AddStack(itemStack);
            AddItemToInventoryGrid(visual);
            visual.OriginContainer = this;
            visual.RestoreOriginal();
            InventoryCursorManager.Instance.FlashRed();
            visual.style.visibility = Visibility.Visible;
            return;
        }

        // AutoPlaceItem already wrote the correct gridX/gridY into itemStack and
        // positioned the visual. Just register the stack and update ownership.
        loots.AddStack(itemStack);
        visual.OriginContainer = loots;
        visual.style.visibility = Visibility.Visible;
    }

    private void DropItemStackToWorld(ItemStack stack)
    {
        if (stack == null) return;

        List<ItemStack> drops = new List<ItemStack>
        {
            stack.Clone()
        };

        Vector3 dropPos = player.transform.position
                          + player.transform.forward;

        _lootManager?.SpawnLootFromStacks(drops, dropPos);

        // 清理 UI / 数据
        stack.RootVisual.RemoveFromHierarchy();
        stack.RootVisual.CleanupRotatedAssets();
    }

    public ItemStack FindStackForAutoStack(ItemStack incoming)
    {
        if (incoming.isInstanced) return null;

        foreach (var stack in ItemStacks)
        {
            if (stack.data.uniqueID != incoming.data.uniqueID)
                continue;

            if (stack.quantity < stack.data.maxStack)
                return stack;
        }

        return null;
    }

    public void OnCursorConfirm()
    {
        if (ContainerRegistry._currentlyDragging == null)
        {
            TryPickItem();
        }
        else
        {
            var dragged   = ContainerRegistry._currentlyDragging;
            var container = InventoryCursorManager.Instance.currentContainer;

            if (container is EquipmentSlotContainer equipSlot &&
                dragged.m_Stack?.data is EquipableItemData equipData &&
                equipSlot.slotType == equipData.equipSlot)
            {
                dragged.RestoreOriginal();
                ContainerRegistry._currentlyDragging = null;
                EquipFromContainer(dragged.m_Stack, equipData);
                InventoryCursorManager.Instance.UpdateInfo();
                return;
            }

            dragged.CursorEndDrag();
            ContainerRegistry._currentlyDragging = null;
        }

        InventoryCursorManager.Instance.UpdateInfo();
    }

    private void TryPickItem()
    {
        var itemStack = InventoryCursorManager.Instance.currentContainer.GetStackAt(InventoryCursorManager.Instance.gridX, InventoryCursorManager.Instance.gridY);
        if (itemStack == null) return;

        var visual = itemStack.RootVisual;

        ContainerRegistry._currentlyDragging = visual;

        if (visual != null)
        {
            visual.CursorBeginDrag();
            if (_uiSounds != null) PlayUISound(_uiSounds.ItemPickUp);
        }
    }

    public void OpenLoots(List<ItemStack> sourceItems, StorageType type)
    {
        //InitCursor();

        ResetCursorToLootStart();
        var stack = loots.GetStackAt(0, 0);
        InventoryCursorManager.Instance.UpdateSizeFromItem(stack);
        loots.OpenLoots(sourceItems, type);

        InventoryCursorManager.Instance.UpdateInfo();
    }

    public void OnRotate(InputAction.CallbackContext ctx)
    {
        if (ContainerRegistry._currentlyDragging == null) return;

        ContainerRegistry._currentlyDragging.ToggleRotate();
    }

    // Loads stored items into UI, placing each if possible
    private async void LoadItems()
    {
        // Wait for UIToolkit grid to be ready.
        await UniTask.WaitUntil(() => m_IsInventoryReady);

        if (GameServices.TryGet<SaveService>(out var save))
            await UniTask.WaitUntil(() => save.IsSaveApplied);


        var failedStacks = new List<ItemStack>();

        foreach (ItemStack itemStacks in ItemStacks)
        {
            ItemVisual inventoryItemVisual = new ItemVisual(itemStacks, this);
            AddItemToInventoryGrid(inventoryItemVisual);

            bool placedSuccessfully = false;

            inventoryItemVisual.ApplyRotation(itemStacks.rotationIndex, revertOnFail: false);

            if (loadMode == LoadMode.UseSavedGridPosition)
            {
                placedSuccessfully = await PlaceItemByGridPosition(inventoryItemVisual, itemStacks.gridX, itemStacks.gridY);
            }
            else if (loadMode == LoadMode.AutoPlaceSequentially)
            {
                placedSuccessfully = await AutoPlaceItem(inventoryItemVisual);
            }

            if (!placedSuccessfully)
            {
                Debug.LogWarning($"Cannot place item {itemStacks.data.itemNameID} using mode {loadMode}");
                RemoveItemFromInventoryGrid(inventoryItemVisual);
                failedStacks.Add(itemStacks);
                continue;
            }

            ConfigureInventoryItem(itemStacks, inventoryItemVisual);
        }

        foreach (var failed in failedStacks)
            ItemStacks.Remove(failed);

        Debug.Log($"[PlayerInventory] LoadItems: done. ItemStacks={ItemStacks.Count}, grid children={m_InventoryGrid.childCount} (includes telegraph)");
        InventoryInit = true;
    }

    public async UniTask RebuildInventoryUI()
    {
        ReConfigureInventoryTelegraph();

        var stacks = new List<ItemStack>(ItemStacks);

        foreach (var stack in stacks)
        {
            if (stack.RootVisual != null)
            {
                stack.RootVisual.RemoveFromHierarchy();
                stack.RootVisual = null;
            }
        }

        foreach (ItemStack itemStacks in ItemStacks)
        {
            ItemVisual inventoryItemVisual = new ItemVisual(itemStacks, this);
            AddItemToInventoryGrid(inventoryItemVisual);

            bool placedSuccessfully = false;

            inventoryItemVisual.ApplyRotation(itemStacks.rotationIndex, revertOnFail: false);

            placedSuccessfully = await PlaceItemByGridPosition(inventoryItemVisual, itemStacks.gridX, itemStacks.gridY);

            if (!placedSuccessfully)
            {
                Debug.LogWarning($"Cannot place item {itemStacks.data.itemNameID} using mode {loadMode}");
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

    public ContainerType GetContainerType()
    {
        return ContainerType.Grids;
    }

    public Task<bool> PlaceItemByGridPosition(VisualElement newItem, int gridX, int gridY)
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
            if (stack.RootVisual == newItem) continue;

            if (GridRectOverlap(gridX, gridY, itemW, itemH, stack.gridX, stack.gridY, stack.RootVisual.GetSlotWidth(), stack.RootVisual.GetSlotHeight()))
            {
                return Task.FromResult(false);
            }
        }

        SetItemPosition(newItem, new Vector2(
            SlotDimension.Width  * gridX,
            SlotDimension.Height * gridY));

        return Task.FromResult(true);
    }

    private void ConfigureSlotDimensions()
    {
        var measured = new Dimensions
        {
            Width  = 75,
            Height = 75
        };

        // Keep own static in sync (used by GetCellRect, PlaceItemByGridPosition, WorldPointToGrid).
        SlotDimension = measured;
        // Push to registry — never write to the SO asset (that corrupts it in the Editor).
        ContainerRegistry.SetMeasuredSlotDimension(measured);
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

    public void ReConfigureInventoryTelegraph()
    {
        if (m_Telegraph != null)
        {
            m_Telegraph.Clear();
        }
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

    // Show placement target for a dragged item. Returns whether it can be placed and the target position
    public (bool canPlace, Vector2 position) ShowPlacementTarget(ItemVisual draggedItem)
    {
        Rect gridRect = m_InventoryGrid.worldBound;
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

        VisualElement targetSlot = m_InventoryGrid.Children()
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

        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_InventoryGrid.worldBound.position;

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
            if (CanStacksMerge(overlapCandidate, draggedItem.m_Stack) && draggedItem.m_Item.maxStack > 1)
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

    private bool GridRectOverlap(int ax, int ay, int aw, int ah,
        int bx, int by, int bw, int bh)
    {
        return ax < bx + bw &&
               ax + aw > bx &&
               ay < by + bh &&
               ay + ah > by;
    }

    public ItemStack GetSameTypeOverlapItem(ItemVisual draggedItem)
    {
        if (draggedItem == null) return null;

        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_InventoryGrid.worldBound.position;

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

            if (!GridRectOverlap(tgx, tgy, tw, th, gx, gy, gw, gh)) continue;

            return CanStacksMerge(stack, draggedItem.m_Stack) ? stack : null;
        }

        return null;
    }

    public ItemStack GetSwapCandidate(ItemVisual draggedItem)
    {
        if (draggedItem == null || m_Telegraph == null) return null;

        Vector2 telePosLocal = m_Telegraph.worldBound.position - m_InventoryGrid.worldBound.position;
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

    private void AddItemToInventoryGrid(VisualElement item)
    {
        m_InventoryGrid.Add(item);
        item.BringToFront();
    }

    public void AddStack(ItemStack stack)
    {
        ItemStacks.Add(stack);
        stack.RootVisual.OriginContainer = this;

        OnItemAdded?.Invoke(stack);
    }

    public void NotifyItemArrived(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return;

        var tempStack = new ItemStack { data = item, quantity = quantity };
        OnItemAdded?.Invoke(tempStack);
    }

    public void RemoveStack(ItemStack stack)
    {
        ItemStacks.Remove(stack);
    }

    // Called on successful extraction. Converts all AuroraDust collectables into permanent currency.
    public void ExtractAuroraDustCollectables()
    {
        var toRemove = new List<ItemStack>();
        int total = 0;

        for (int i = 0; i < ItemStacks.Count; i++)
        {
            if (ItemStacks[i].data is CollectableItemData c && c.isAuroraDust)
            {
                total += ItemStacks[i].quantity;
                toRemove.Add(ItemStacks[i]);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
            RemoveItemStack(toRemove[i]);

        if (total > 0)
            AddAuraDust(total);
    }

    // Removes 'penalty' units from AuroraDust collectable stacks directly,
    // consuming stacks in order until the full amount is deducted.
    public void ConsumeDustPenalty(int penalty)
    {
        int remaining = penalty;

        for (int i = 0; i < ItemStacks.Count && remaining > 0; i++)
        {
            if (!(ItemStacks[i].data is CollectableItemData c) || !c.isAuroraDust) continue;
            int remove = Mathf.Min(ItemStacks[i].quantity, remaining);
            ItemStacks[i].quantity -= remove;
            remaining -= remove;
        }

        for (int i = ItemStacks.Count - 1; i >= 0; i--)
        {
            if (ItemStacks[i].data is CollectableItemData cd && cd.isAuroraDust && ItemStacks[i].quantity <= 0)
                RemoveItemStack(ItemStacks[i]);
        }
    }

    public void RemoveItemStack(ItemStack itemStack)
    {
        RemoveStack(itemStack);
        RemoveItemFromInventoryGrid(itemStack.RootVisual);
    }

    private void RemoveItemFromInventoryGrid(VisualElement item)
    {
        if (item != null && item.parent == m_InventoryGrid)
        {
            m_InventoryGrid.Remove(item);
        }
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
        if (m_InventoryGrid == null) return false;
        return m_InventoryGrid.worldBound.Contains(worldPoint);
    }

    public (int gridX, int gridY) WorldPointToGrid(Vector2 worldPoint)
    {
        Vector2 local = worldPoint - m_InventoryGrid.worldBound.position;

        float fx = local.x / SlotDimension.Width;
        float fy = local.y / SlotDimension.Height;

        int gx = Mathf.RoundToInt(fx);
        int gy = Mathf.RoundToInt(fy);

        return (gx, gy);
    }

    public static void UpdateItemDetails(ItemData item)
    {
        //m_ItemDetailHeader.text = item.FriendlyName;
        //m_ItemDetailBody.text = item.Description;
        //m_ItemDetailPrice.text = item.SellPrice.ToString();
    }

    public ItemStack GetStackUnderTelegraph()
    {
        if (m_Telegraph == null) return null;

        return ItemStacks.Where(x => x.RootVisual != null && x.RootVisual.layout.Overlaps(m_Telegraph.layout)).ToArray()[0];
    }

    public void OnToggleEquip(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
        {
            _isXButtonHeld = false;
            CancelConsumeHold();
            return;
        }

        if (!ctx.performed) return;

        // While dragging: X equips to the current slot if compatible, otherwise ignore
        var dragging = ContainerRegistry._currentlyDragging;
        if (dragging != null)
        {
            var dragTarget = InventoryCursorManager.Instance.currentContainer;
            if (dragTarget is EquipmentSlotContainer targetEquipSlot &&
                dragging.m_Stack?.data is EquipableItemData dragEquipData &&
                targetEquipSlot.slotType == dragEquipData.equipSlot)
            {
                dragging.RestoreOriginal();
                ContainerRegistry._currentlyDragging = null;
                EquipFromContainer(dragging.m_Stack, dragEquipData);
                InventoryCursorManager.Instance.UpdateInfo();
            }
            return;
        }

        var container = InventoryCursorManager.Instance.currentContainer;
        var stack     = container.GetStackAt(InventoryCursorManager.Instance.gridX, InventoryCursorManager.Instance.gridY);

        if (stack == null) return;

        if (container is ConsumableSlotContainer)
        {
            _isXButtonHeld = true;
            BeginConsumeHold(stack, container, InventoryCursorManager.Instance.m_Cursor.worldBound);
            return;
        }

        if (stack.data is ConsumableItemData)
        {
            _isXButtonHeld = true;
            BeginConsumeHold(stack, container, InventoryCursorManager.Instance.m_Cursor.worldBound);
            return;
        }

        if (stack.data is not EquipableItemData equipData) return;

        if (container is EquipmentSlotContainer equipSlot)
            _ = UnequipToInventory(stack, equipSlot);
        else
            EquipFromContainer(stack, equipData);
    }

    private void UseConsumableFromInventory(ItemStack stack, IItemContainer container)
    {
        if (player == null)
        {
            Debug.LogWarning("[PlayerInventory] Cannot use consumable — player reference is null.");
            return;
        }

        bool used = stack.Use(player);
        if (!used) return;

        if (stack.IsEmpty)
            container.RemoveItemStack(stack);
        else
            stack.RootVisual.UpdateCountLabel();
    }

    private void BeginConsumeHold(ItemStack stack, IItemContainer container, Rect worldRect)
    {
        // Guard: ignore if a hold is already in progress to prevent timer resets from spam.
        // CompleteConsumeHold sets _isConsumeHolding=false before calling here, so restarts pass through.
        if (_isConsumeHolding) return;

        _isConsumeHolding     = true;
        _consumeHoldTime      = 0f;
        _consumeHoldStack     = stack;
        _consumeHoldContainer = container;
        RadialProgressOverlay.Instance.Show(worldRect);
    }

    private void CancelConsumeHold()
    {
        if (!_isConsumeHolding) return;
        _isConsumeHolding     = false;
        _isXButtonHeld        = false;
        _consumeHoldStack     = null;
        _consumeHoldContainer = null;
        RadialProgressOverlay.Instance.Hide();
    }

    private void CompleteConsumeHold()
    {
        _isConsumeHolding     = false;
        var stack             = _consumeHoldStack;
        var container         = _consumeHoldContainer;
        var lastCursorRect    = InventoryCursorManager.Instance.m_Cursor.worldBound;
        _consumeHoldStack     = null;
        _consumeHoldContainer = null;
        RadialProgressOverlay.Instance.Hide();

        if (stack != null && container != null)
            UseConsumableFromInventory(stack, container);

        // If X is still held and the stack still has charges, restart the cycle.
        if (_isXButtonHeld && stack != null && !stack.IsEmpty)
            BeginConsumeHold(stack, container, lastCursorRect);
    }

    private void TickConsumeHold()
    {
        if (!_isConsumeHolding) return;

        _consumeHoldTime += Time.deltaTime;
        float t = Mathf.Clamp01(_consumeHoldTime / _holdConsumeDuration);
        RadialProgressOverlay.Instance.SetProgress(t);

        if (t >= 1f) CompleteConsumeHold();
    }

    private async void UnequipConsumableToInventory(ItemStack stack, ConsumableSlotContainer slot)
    {
        var visual = stack.RootVisual;
        if (visual == null) return;

        slot.RemoveStack(stack);
        slot.GetInventoryGrid().Remove(visual);

        GetInventoryGrid().Add(visual);

        bool placed = await AutoPlaceItem(visual);
        if (!placed)
        {
            // No space — restore to slot.
            slot.AddStack(stack);
            slot.GetInventoryGrid().Add(visual);
            visual.OriginContainer = slot;
            visual.RestoreOriginal();
            return;
        }

        AddStack(stack);
        visual.OriginContainer = this;
        visual.style.visibility = Visibility.Visible;
    }

    private async void EquipFromContainer(ItemStack stack, EquipableItemData equipData)
    {
        var visual = stack.RootVisual;
        if (visual == null) return;

        var equipSlot = ContainerRegistry.GetEquipmentSlot(equipData.equipSlot);
        if (equipSlot == null) return;

        var oldStack = equipSlot.EquippedStack;

        bool sendOldToLoot   = false;
        bool dropOldToWorld  = false;

        if (oldStack != null)
        {
            bool canFitInInventory = await CanFitItemAsync(oldStack, ignoreStack: stack);

            if (!canFitInInventory)
            {
                if (loots.isActive && await CanFitInLootAsync(oldStack))
                    sendOldToLoot = true;
                else
                    dropOldToWorld = true; // No space anywhere — discard to world.
            }
        }

        var container = visual.OriginContainer;
        container.RemoveStack(stack);
        container.GetInventoryGrid().Remove(visual);

        if (oldStack != null)
        {
            if (sendOldToLoot)
                await UnequipToLoot(oldStack, equipSlot);
            else if (dropOldToWorld)
            {
                equipSlot.RemoveStack(oldStack);
                equipSlot.GetInventoryGrid().Remove(oldStack.RootVisual);
                DropItemStackToWorld(oldStack);
            }
            else
                await UnequipToInventory(oldStack, equipSlot, skipCheck: true);
        }

        ContainerRegistry.TransferVisualBetweenContainers(stack, equipSlot.GetInventoryGrid(), 0, 0);

        equipSlot.AddStack(stack);
        visual.OriginContainer = equipSlot;
        visual.style.visibility = Visibility.Visible;
    }

    public async Task<bool> CanFitItemAsync(ItemStack stack, ItemStack ignoreStack = null)
    {
        var tempVisual = new ItemVisual(stack, this);
        GetInventoryGrid().Add(tempVisual);

        ItemStack removed = null;
        if (ignoreStack != null && ItemStacks.Contains(ignoreStack))
        {
            removed = ignoreStack;
            ItemStacks.Remove(ignoreStack);
        }

        bool canFit = await AutoPlaceItem(tempVisual);

        // 👇 恢复
        if (removed != null)
        {
            ItemStacks.Add(removed);
        }

        tempVisual.RemoveFromHierarchy();

        return canFit;
    }

    public async Task<bool> UnequipToInventory(
        ItemStack stack,
        EquipmentSlotContainer equipSlot,
        bool skipCheck = false,
        ItemStack ignoreStack = null
    )
    {
        if (stack == null || equipSlot == null) return false;

        var visual = stack.RootVisual;
        if (visual == null) return false;

        var inventory = PlayerInventory.Instance;

        // 🔴 1. 检测（可跳过）
        if (!skipCheck)
        {
            bool canFit = await inventory.CanFitItemAsync(stack, ignoreStack);
            if (!canFit)
            {
                Debug.Log("背包满，无法卸下装备");
                InventoryCursorManager.Instance.FlashRed();
                return false;
            }
        }

        // 🟡 2. 从装备槽移除
        equipSlot.RemoveStack(stack);

        // 🟡 3. UI移除
        equipSlot.GetInventoryGrid().Remove(visual);

        // 🟡 4. 转移
        ContainerRegistry.TransferVisualBetweenContainers(
            stack,
            inventory.GetInventoryGrid(),
            0, 0
        );

        // 🟡 5. 放置
        bool placed = await inventory.AutoPlaceItem(visual);

        if (!placed)
        {
            Debug.LogError("检测通过但放置失败 → 状态异常");

            // 🔁 回滚
            ContainerRegistry.TransferVisualBetweenContainers(
                stack,
                equipSlot.GetInventoryGrid(),
                0, 0
            );

            equipSlot.AddStack(stack);
            return false;
        }

        // 🟢 6. 加入数据
        inventory.AddStack(stack);

        visual.OriginContainer = inventory;
        visual.style.visibility = Visibility.Visible;

        return true;
    }

    // Checks whether stack fits somewhere in the active loot container without modifying state.
    private async Task<bool> CanFitInLootAsync(ItemStack stack)
    {
        var tempVisual = new ItemVisual(stack, this);
        loots.GetInventoryGrid().Add(tempVisual);
        bool canFit = await loots.AutoPlaceItem(tempVisual);
        tempVisual.RemoveFromHierarchy();
        return canFit;
    }

    // Moves an equipped item from its slot into the active loot container.
    private async Task<bool> UnequipToLoot(ItemStack stack, EquipmentSlotContainer equipSlot)
    {
        var visual = stack.RootVisual;
        if (visual == null) return false;

        equipSlot.RemoveStack(stack);
        equipSlot.GetInventoryGrid().Remove(visual);

        ContainerRegistry.TransferVisualBetweenContainers(stack, loots.GetInventoryGrid(), 0, 0);

        bool placed = await loots.AutoPlaceItem(visual);

        if (!placed)
        {
            // Rollback to equipment slot.
            ContainerRegistry.TransferVisualBetweenContainers(stack, equipSlot.GetInventoryGrid(), 0, 0);
            equipSlot.AddStack(stack);
            return false;
        }

        loots.AddStack(stack);
        visual.OriginContainer = loots;
        visual.style.visibility = Visibility.Visible;

        return true;
    }

    public void CaptureToSave(SaveData data)
    {
        data.inventory.auroraDust = _auroraDust;
        data.inventory.inventoryStacks.Clear();

        foreach (var itemStack in ItemStacks)
        {
            if (itemStack?.data == null) continue;

            data.inventory.inventoryStacks.Add(ItemStackSaveHelper.Capture(itemStack));
        }
    }

    public void ApplyFromSave(SaveData data)
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        _auroraDust = data.inventory.auroraDust;
        OnAuraDustChanged?.Invoke();

        // Remove any visuals from a previous load before clearing the data list.
        for (int i = 0; i < ItemStacks.Count; i++)
        {
            if (ItemStacks[i].RootVisual != null)
                RemoveItemFromInventoryGrid(ItemStacks[i].RootVisual);
        }

        ItemStacks.Clear();

        foreach (var entry in data.inventory.inventoryStacks)
        {
            var itemData = save.ResolveItem(entry.uniqueID);
            if (itemData == null) continue;

            ItemStacks.Add(ItemStackSaveHelper.Restore(entry, itemData));
        }

        loadMode = LoadMode.UseSavedGridPosition;
    }

    private static bool CanStacksMerge(ItemStack a, ItemStack b)
    {
        if (a == null || b == null) return false;
        if (a.data == null || b.data == null) return false;

        if (a.isInstanced || b.isInstanced)
            return false;

        return a.data == b.data || a.data.uniqueID == b.data.uniqueID;
    }

    public void OnPress(InputAction.CallbackContext ctx)
    {
        if (ContainerRegistry._currentlyDragging != null) return;

        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, loots) && loots.isActive)
        {
            StartSequentialLoot();
            return;
        }

        isHolding      = true;
        holdTime       = 0f;
        _holdTriggered = false;

        var root = GetCurrentRoot();
        if (root != null)
            root.style.display = DisplayStyle.None;

        var fill = GetCurrentFill();
        if (fill != null)
            fill.style.width = Length.Percent(0);
    }

    public void OnRelease(InputAction.CallbackContext ctx)
    {
        if (_isSequentialLooting)
        {
            StopSequentialLoot();
            return;
        }

        if (!isHolding) return;

        isHolding = false;
        progressBar_root_Inventory.style.display = DisplayStyle.None;
        progressBar_root_Loot.style.display      = DisplayStyle.None;

        if (_holdTriggered)
        {
            _holdTriggered = false;
            return;
        }

        bool success = holdTime >= delayTime + holdDuration;
        if (!success) return;

        if (_uiSounds != null) PlayUISound(_uiSounds.LootAllComplete);
        _ = ExecuteTransfer();
    }

    private VisualElement GetCurrentRoot()
    {
        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, this))
            return progressBar_root_Inventory;

        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, loots))
            return progressBar_root_Loot;

        return null;
    }

    private VisualElement GetCurrentFill()
    {
        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, this))
            return progressBar_fill_Inventory;

        if (ReferenceEquals(InventoryCursorManager.Instance.currentContainer, loots))
            return progressBar_fill_Loot;

        return null;
    }

    private void PlayUISound(FMODUnity.EventReference sound)
    {
        if (_audio == null || sound.IsNull) return;
        _audio.PlayOneShot(sound, Vector3.zero);
    }

    #region Sequential Loot

    private void StartSequentialLoot()
    {
        if (_isSequentialLooting) return;
        _isSequentialLooting = true;
        _sequentialLootCts   = new CancellationTokenSource();
        SequentialLootAsync(_sequentialLootCts.Token).Forget();
    }

    private void StopSequentialLoot()
    {
        _sequentialLootCts?.Cancel();
        _sequentialLootCts   = null;
        _isSequentialLooting = false;
        RadialProgressOverlay.Instance.Hide();
    }

    private async UniTaskVoid SequentialLootAsync(CancellationToken ct)
    {
        try
        {
            // Sort all stacks in reading order (top-left → bottom-right)
            var cursorStack = loots.GetStackAt(
                InventoryCursorManager.Instance.gridX,
                InventoryCursorManager.Instance.gridY);

            int gridW = loots.GetGridWidth();
            var sorted = new List<ItemStack>(loots.ItemStacks);
            sorted.Sort((a, b) => (a.gridY * gridW + a.gridX).CompareTo(b.gridY * gridW + b.gridX));

            // Rotate list so it starts from the cursor item
            int startIndex = cursorStack != null ? sorted.IndexOf(cursorStack) : 0;
            if (startIndex < 0) startIndex = 0;
            var stacks = new List<ItemStack>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
                stacks.Add(sorted[(startIndex + i) % sorted.Count]);

            foreach (var stack in stacks)
            {
                if (ct.IsCancellationRequested) break;
                if (stack?.RootVisual == null) continue;

                int dimW  = stack.data.SlotDimension.Width;
                int dimH  = stack.data.SlotDimension.Height;
                float delay = _baseLootDelay + _extraDimensionDelay * ((dimW - 1) + (dimH - 1));

                var cursor = InventoryCursorManager.Instance;
                cursor.currentContainer = loots;
                cursor.gridX = stack.gridX;
                cursor.gridY = stack.gridY;
                cursor.MoveTo(loots.GetCellRect(stack.gridX, stack.gridY));

                RadialProgressOverlay.Instance.Show(loots.GetCellRect(stack.gridX, stack.gridY));

                float elapsed = 0f;
                while (elapsed < delay && !ct.IsCancellationRequested)
                {
                    elapsed += Time.deltaTime;
                    RadialProgressOverlay.Instance.SetProgress(elapsed / delay);
                    await UniTask.NextFrame(cancellationToken: ct);
                }

                if (ct.IsCancellationRequested) break;

                RadialProgressOverlay.Instance.Hide();
                if (_uiSounds != null) PlayUISound(_uiSounds.ItemPickUp);
                await TransferSingleStackToInventory(stack);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isSequentialLooting = false;
            _sequentialLootCts   = null;
            RadialProgressOverlay.Instance.Hide();
        }
    }

    private async UniTask TransferSingleStackToInventory(ItemStack stack)
    {
        if (stack == null) return;
        if (!loots.ItemStacks.Contains(stack)) return;

        if (stack.RootVisual != null)
        {
            stack.RootVisual.RemoveFromHierarchy();
            stack.RootVisual.CleanupRotatedAssets();
        }
        loots.ItemStacks.Remove(stack);

        bool success = await TryTransferStackToInventory(stack);

        if (!success)
        {
            bool restored = await TryTransferStackToLoot(stack);
            if (!restored)
                Debug.LogWarning($"[PlayerInventory] Item lost during sequential loot: {stack.data?.itemNameID}");
        }
    }

    #endregion
}
