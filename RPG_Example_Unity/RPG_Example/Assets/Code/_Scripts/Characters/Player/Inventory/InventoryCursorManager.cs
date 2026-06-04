using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryCursorManager : MonoBehaviour
{
    public static InventoryCursorManager Instance;

    [SerializeField] private InputIconInventory _inputIconInventory;

    private float m_FlashDuration = 0.2f;
    private float m_FlashInterval = 0.05f;

    public VisualElement m_Cursor;
    public int gridX;
    public int gridY;
    public IItemContainer currentContainer;
    public Vector2 originalLocalPos;
    public IItemContainer originalContainer;

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
    }

    private void Update()
    {
        if (m_Cursor == null) return;

        
        var navC = currentContainer as INavigatableContainer;
        if (navC == null) return;

        var stack = navC.GetStackAt(gridX, gridY);

        bool isDragging = ContainerRegistry._currentlyDragging != null;

        if (stack != null && !isDragging)
        {
            if (gridX != stack.gridX || gridY != stack.gridY)
            {
                gridX = stack.gridX;
                gridY = stack.gridY;
                MoveTo(navC.GetCellRect(gridX, gridY));
            }
        }


        UpdateSizeFromItem(stack);

        // Equipment slots can resize when a different-size weapon is equipped.
        // Refresh cursor position from the live cell rect so it tracks the new slot bounds.
        if (!isDragging && currentContainer is EquipmentSlotContainer)
            MoveTo(navC.GetCellRect(gridX, gridY));
    }

    #region Public API

    public void UpdateSizeFromItem(ItemStack stack)
    {
        INavigatableContainer navigatableContainer = currentContainer as INavigatableContainer;
        var slotSize = navigatableContainer.GetCellRect(gridX, gridY);

        if (stack == null || stack.RootVisual == null)
        {
            m_Cursor.style.width = slotSize.width;
            m_Cursor.style.height = slotSize.height;
            return;
        }

        int w = stack.RootVisual.GetSlotWidth();
        int h = stack.RootVisual.GetSlotHeight();

        m_Cursor.style.width = w * ContainerRegistry.SlotDimension.Width;
        m_Cursor.style.height = h * ContainerRegistry.SlotDimension.Height;
        
    }

    public void MoveTo(Rect rect)
    {
        m_Cursor.style.left = rect.x-12.25f;
        m_Cursor.style.top = rect.y-95;

        UpdateInfo();
    }

    public Rect GetCurrentCellRect()
    {
        var nav = currentContainer as INavigatableContainer;
        return nav == null ? default : nav.GetCellRect(gridX, gridY);
    }

    public void HideBorder() => m_Cursor.style.display = DisplayStyle.None;
    public void ShowBorder() => m_Cursor.style.display = DisplayStyle.Flex;

    public void UpdateInfo()
    {
        ItemDescription.Instance.UpdateDescription();
        _inputIconInventory?.UpdateInputActionIcon();
    }

    private bool m_IsFlashing = false;

    public async void FlashRed()
    {
        if (m_Cursor == null) return;

        // ❗关键：正在闪就直接返回
        if (m_IsFlashing) return;

        m_IsFlashing = true;

        float timer = 0f;
        bool toggle = false;

        Color normalColor = m_Cursor.resolvedStyle.borderTopColor;
        Color flashColor = Color.red;

        while (timer < m_FlashDuration)
        {
            toggle = !toggle;

            SetBorderColor(toggle ? flashColor : normalColor);

            await Cysharp.Threading.Tasks.UniTask.Delay(
                (int)(m_FlashInterval * 1000)
            );

            timer += m_FlashInterval;
        }

        // 恢复原颜色
        SetBorderColor(normalColor);

        m_IsFlashing = false;
    }

    private void SetBorderColor(Color color)
    {
        m_Cursor.style.borderTopColor = color;
        m_Cursor.style.borderBottomColor = color;
        m_Cursor.style.borderLeftColor = color;
        m_Cursor.style.borderRightColor = color;
    }

    #endregion


}