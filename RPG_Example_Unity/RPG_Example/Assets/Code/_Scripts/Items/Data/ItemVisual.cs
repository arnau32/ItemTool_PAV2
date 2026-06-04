using UnityEngine;
using UnityEngine.UIElements;

public class ItemVisual : VisualElement
{
    public ItemData m_Item;
    public ItemStack m_Stack;
    public IItemContainer OriginContainer;
    public IItemContainer HoverContainer;
    private VisualElement _icon;
    private Vector2 m_OriginalPosition;
    private int m_OriginalRotateIndex;
    private bool m_IsDragging;
    public bool IsDragging => m_IsDragging;
    private (bool canPlace, Vector2 position) m_PlacementResults;

    // rotation index: 0 -> 0deg, 1 -> 90deg, 2 -> 180deg, 3 -> 270deg
    public int _rotationIndex = 0;
    // original slot dims (cached from data for clarity)
    private readonly int _origSlotW;
    private readonly int _origSlotH;

    // --- Sprite rotation cache (index 0..3) ---
    private Sprite[] _rotatedSprites = new Sprite[4]; // index 0 => original sprite (we'll set it)
    private Texture2D[] _rotatedTextures = new Texture2D[4]; // keep Texture2D we create so we can Destroy them later

    private Label _labelCount;

    public ItemVisual(ItemStack itemStack, IItemContainer itemContainer)
    {
        OriginContainer = itemContainer;
        m_Stack = itemStack;
        m_Item = itemStack.data;

        _origSlotW = m_Item.SlotDimension.Width;
        _origSlotH = m_Item.SlotDimension.Height;

        name = $"{m_Item.itemNameID}";

        style.height = m_Item.SlotDimension.Height * ContainerRegistry.SlotDimension.Height;
        style.width = m_Item.SlotDimension.Width * ContainerRegistry.SlotDimension.Width;

        style.visibility = Visibility.Hidden;

        // create icon element and add it
        _icon = new VisualElement();
        _icon.style.backgroundImage = new StyleBackground(m_Item.icon);

        // center / fill the icon inside container via percentages
        _icon.style.width = Length.Percent(100);
        _icon.style.height = Length.Percent(100);
        _icon.style.unityOverflowClipBox = OverflowClipBox.ContentBox; // optional

        Add(_icon);

        _icon.AddToClassList("visual-icon");
        AddToClassList("visual-icon-container");

        style.unityBackgroundImageTintColor = RarityColorProvider.RarityColorConfig.GetColor(m_Stack.GetEffectiveRarity());

        _labelCount = new Label();
        _labelCount.text = m_Stack.quantity.ToString();
        _labelCount.AddToClassList("stack-Count");

        Add(_labelCount);
    }

    ~ItemVisual()
    {
        CleanupRotatedAssets();
    }

    public void SetPlacementResults((bool canPlace, Vector2 position) result)
    {
        m_PlacementResults = result;
    }

    public bool ToggleRotate(bool revertOnFail = false)
    {
        int newIndex = (_rotationIndex + 1) % 4;
        return ApplyRotation(newIndex, revertOnFail);
    }

    public bool ApplyRotation(int newIndex, bool revertOnFail)
    {

        // Backup current visual/layout state (for revert)
        float oldWidth = this.resolvedStyle.width;
        float oldHeight = this.resolvedStyle.height;
        int oldIndex = _rotationIndex;

        // Determine slot counts for new rotation.
        // For indices 1 and 3 (90° or 270°) we swap slot width/height.
        bool isQuarter = (newIndex % 2) == 1;
        int slotCountW = isQuarter ? _origSlotH : _origSlotW;
        int slotCountH = isQuarter ? _origSlotW : _origSlotH;

        // Compute pixel sizes using global SlotDimension
        float newWidth = slotCountW * ContainerRegistry.SlotDimension.Width;
        float newHeight = slotCountH * ContainerRegistry.SlotDimension.Height;

        // Apply new layout sizes (affects layout/Overlaps checks)
        style.width = newWidth;
        style.height = newHeight;

        // Apply visual rotation to the icon (angle in degrees)
        ApplySpriteRotation(newIndex);

        // Update rotation index
        _rotationIndex = newIndex;

        // Refresh placement preview (telegraph)
        m_PlacementResults = OriginContainer.ShowPlacementTarget(this);

        // If we need to revert on fail and placement preview says cannot place, revert
        if (revertOnFail && !m_PlacementResults.canPlace)
        {
            // revert layout and visual rotation
            style.width = oldWidth;
            style.height = oldHeight;
            _icon.style.rotate = new Rotate(new Angle(oldIndex * 90f, AngleUnit.Degree));
            _rotationIndex = oldIndex;

            // refresh telegraph for reverted state (optional)
            m_PlacementResults = OriginContainer.ShowPlacementTarget(this);
            return false;
        }

        return true;
    }

    // Sets the local UI position (left/top) of this element
    public void SetPosition(Vector2 pos)
    {
        style.left = pos.x;
        style.top = pos.y;
    }

    // Called when mouse button is released
    public void CursorEndDrag()
    {
        if (!m_IsDragging)
        {
            return;
        }

        m_IsDragging = false;

        if (InventoryCursorManager.Instance.m_Cursor != null)
            InventoryCursorManager.Instance.ShowBorder();


        // Find which container is under pointer
        if (m_PlacementResults.canPlace)
        {
            SetPosition(new Vector2(
                m_PlacementResults.position.x - parent.worldBound.position.x,
                m_PlacementResults.position.y - parent.worldBound.position.y));


            Vector2 screenPos = Input.mousePosition;
            Vector2 panelPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

            var targetContainer = ContainerRegistry.GetContainerAtPoint(panelPos);

            ContainerRegistry.TransferStack(m_Stack, OriginContainer, m_Stack.RootVisual.HoverContainer);
            if (OriginContainer == targetContainer&& targetContainer.GetContainerType() != ContainerType.Grids)
            {
                ApplyRotation(0, false);
            }
            UpdateGridCoordinates();
            OriginContainer.EndDrag();

            return;
        }

        ItemStack overlapStack = HoverContainer.GetSameTypeOverlapItem(this);

        if (overlapStack != null)
        {
            int maxStack = overlapStack.data.maxStack;
            int total = overlapStack.quantity + m_Stack.quantity;

            if (total <= maxStack)
            {
                overlapStack.quantity = total;

                overlapStack.RootVisual.UpdateCountLabel();

                // Notify PlayerInventory when items merge via drag-drop stacking.
                // AddStack is not called in this path, so OnItemAdded never fires.
                if (HoverContainer is PlayerInventory inv)
                    inv.NotifyItemArrived(m_Stack.data, m_Stack.quantity);

                OriginContainer.RemoveItemStack(m_Stack);

                this.RemoveFromHierarchy();
                CleanupRotatedAssets();
                OriginContainer.EndDrag();

                return;
            }
            else
            {
                int overflow = total - maxStack;
                int moved    = m_Stack.quantity - overflow;

                overlapStack.quantity = maxStack; 
                m_Stack.quantity = overflow;

                overlapStack.RootVisual.UpdateCountLabel();
                this.UpdateCountLabel();

                // Partial stack merge — notify for the portion that entered inventory.
                if (moved > 0 && HoverContainer is PlayerInventory inv2)
                    inv2.NotifyItemArrived(m_Stack.data, moved);
            }
        }

        if (overlapStack == null)
        {
            var swapCandidate = HoverContainer?.GetSwapCandidate(this);
            if (swapCandidate != null)
            {
                ExecuteSwap(swapCandidate);
                return;
            }
        }

        RestoreOriginal();
    }

    private void ExecuteSwap(ItemStack target)
    {
        (int origGX, int origGY) = OriginContainer.WorldPointToGrid(m_OriginalPosition);
        int targetGX = target.gridX;
        int targetGY = target.gridY;

        var originGrid = OriginContainer.GetInventoryGrid();
        var hoverGrid  = HoverContainer.GetInventoryGrid();

        if (OriginContainer == HoverContainer)
        {
            ContainerRegistry.TransferVisualBetweenContainers(m_Stack, originGrid, targetGX, targetGY);
            ContainerRegistry.TransferVisualBetweenContainers(target, originGrid, origGX, origGY);
        }
        else
        {
            OriginContainer.RemoveStack(m_Stack);
            HoverContainer.RemoveStack(target);

            ContainerRegistry.TransferVisualBetweenContainers(m_Stack, hoverGrid, targetGX, targetGY);
            ContainerRegistry.TransferVisualBetweenContainers(target, originGrid, origGX, origGY);

            HoverContainer.AddStack(m_Stack);
            OriginContainer.AddStack(target);
        }

        m_Stack.rotationIndex = _rotationIndex;

        HoverContainer.HideTelegraph();
        m_IsDragging = false;
        OriginContainer.EndDrag();
        InventoryCursorManager.Instance?.ShowBorder();
    }

    public void RestoreOriginal()
    {
        OriginContainer.EndDrag();
        (int gx, int gy) = OriginContainer.WorldPointToGrid(m_OriginalPosition);

        ContainerRegistry.TransferVisualBetweenContainers(m_Stack, OriginContainer.GetInventoryGrid(), gx, gy);
        ContainerRegistry.TransferStack(m_Stack, OriginContainer, OriginContainer);


        Vector2 localPosition = m_OriginalPosition - parent.worldBound.position;

        SetPosition(localPosition);
        ApplyRotation(m_OriginalRotateIndex, false);
        m_IsDragging = false;
        BringToFront();
        InventoryCursorManager.Instance?.ShowBorder();
    }
    // Compute original local position relative to parent
    public void CursorBeginDrag()
    {
        m_IsDragging = true;

        m_OriginalPosition = worldBound.position;
        m_OriginalRotateIndex = _rotationIndex;

        InventoryCursorManager.Instance?.HideBorder();

        BringToFront();
        OriginContainer.BeginDrag(this);
    }


    // Called when mouse moves. If dragging, update position and compute possible placement
    private void OnMouseMoveEvent(MouseMoveEvent mouseEvent)
    {
        if (!m_IsDragging) { return; }

        //SetPosition(GetMousePosition(mouseEvent.mousePosition));
        //m_PlacementResults = OriginContainer.ShowPlacementTarget(this);
        /*
        ItemStack OverlapStack = HoverContainer.GetSameTypeOverlapItem(this);
        if (OverlapStack != null)
        {
        }*/
    }

    // Converts the mouse pointer position to a local position for this element (centering logic)
    public Vector2 GetMousePosition(Vector2 mousePosition)
    {
        return new Vector2(mousePosition.x - (layout.width / 2) -
        parent.worldBound.position.x, mousePosition.y - (layout.height / 2) -
        parent.worldBound.position.y);
    }

    private void UpdateGridCoordinates()
    {

        // update grid coords (top-left cell)
        Vector2 localPos = new Vector2(worldBound.position.x - parent.worldBound.position.x, worldBound.position.y - parent.worldBound.position.y);

        m_Stack.gridX = Mathf.RoundToInt(localPos.x / ContainerRegistry.SlotDimension.Width);
        m_Stack.gridY = Mathf.RoundToInt(localPos.y / ContainerRegistry.SlotDimension.Height);

        // store rotation index for persistence
        m_Stack.rotationIndex = _rotationIndex; // add rotationIndex to ItemStack if not exists
    }

    private void EnsureRotatedSprite(int rotationIndex)
    {
        if (rotationIndex < 0 || rotationIndex > 3) rotationIndex = ((rotationIndex % 4) + 4) % 4;

        // cache original sprite at index 0
        if (_rotatedSprites[0] == null)
            _rotatedSprites[0] = m_Item.icon;

        if (rotationIndex == 0) return;
        if (_rotatedSprites[rotationIndex] != null) return; // already created

        Sprite srcSprite = m_Item.icon;
        if (srcSprite == null) return;

        Texture2D srcTex = srcSprite.texture;

        // sprite may be part of an atlas; get its rect
        Rect srcRect = srcSprite.rect;
        int srcX = Mathf.FloorToInt(srcRect.x);
        int srcY = Mathf.FloorToInt(srcRect.y);
        int srcW = Mathf.FloorToInt(srcRect.width);
        int srcH = Mathf.FloorToInt(srcRect.height);

        if (!srcTex.isReadable)
        {
            Debug.LogWarning($"[ItemVisual] Icon texture '{srcTex.name}' is not readable — " +
                             "enable Read/Write in the texture importer to generate rotated sprites.");
            return;
        }

        Color[] pixels = srcTex.GetPixels(srcX, srcY, srcW, srcH);

        int dstW = srcW;
        int dstH = srcH;
        if (rotationIndex == 1 || rotationIndex == 3)
        {
            dstW = srcH;
            dstH = srcW;
        }

        Color[] rotatedPixels = new Color[dstW * dstH];

        // Map source (x,y) -> dest (newX,newY) then linear index = newY * dstW + newX
        for (int y = 0; y < srcH; y++)
        {
            for (int x = 0; x < srcW; x++)
            {
                Color c = pixels[y * srcW + x];
                int newX = 0;
                int newY = 0;

                switch (rotationIndex)
                {
                    case 1: // 90° CW: (x,y) -> (srcH - 1 - y, x)
                        newX = srcH - 1 - y;
                        newY = x;
                        break;
                    case 2: // 180°: (x,y) -> (srcW - 1 - x, srcH - 1 - y)
                        newX = srcW - 1 - x;
                        newY = srcH - 1 - y;
                        break;
                    case 3: // 270° CW: (x,y) -> (y, srcW - 1 - x)
                        newX = y;
                        newY = srcW - 1 - x;
                        break;
                }

                int dstIndex = newY * dstW + newX;
                // safety check (shouldn't happen if math is correct)
                if (dstIndex < 0 || dstIndex >= rotatedPixels.Length)
                {
                    Debug.LogError($"Rotation mapping out of range: r={rotationIndex} srcW={srcW} srcH={srcH} x={x} y={y} -> newX={newX} newY={newY} dstW={dstW} dstH={dstH} dstIndex={dstIndex}");
                    continue;
                }

                rotatedPixels[dstIndex] = c;
            }
        }

        // create destination texture and sprite
        Texture2D dstTex = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
        dstTex.SetPixels(rotatedPixels);
        dstTex.Apply();

        Sprite dstSprite = Sprite.Create(dstTex, new Rect(0, 0, dstW, dstH),
            new Vector2(0.5f, 0.5f), srcSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);

        _rotatedTextures[rotationIndex] = dstTex;
        _rotatedSprites[rotationIndex] = dstSprite;
    }


    // Apply rotated sprite to the icon background (swap for index 0..3).
    private void ApplySpriteRotation(int rotationIndex)
    {
        if (_rotatedSprites[0] == null) _rotatedSprites[0] = m_Item.icon;

        EnsureRotatedSprite(rotationIndex);

        if (_rotatedSprites[rotationIndex] != null)
        {
            // Physical rotated sprite available — sprite itself carries the rotation, no CSS needed.
            _icon.style.backgroundImage = new StyleBackground(_rotatedSprites[rotationIndex]);
            _icon.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
        }
        else
        {
            // Texture was not readable — use CSS rotation on the original sprite as visual fallback.
            // Items with non-square icons may not look pixel-perfect; mark texture Read/Write to fix.
            _icon.style.backgroundImage = new StyleBackground(m_Item.icon);
            _icon.style.rotate = new Rotate(new Angle(rotationIndex * 90f, AngleUnit.Degree));
        }
    }


    public void CleanupRotatedAssets()
    {
        for (int i = 1; i < 4; i++)
        {
            if (_rotatedSprites[i] != null)
            {
                // if we created the Sprite, destroy it
                if (_rotatedTextures[i] != null)
                {
                    // Destroy created sprite and texture
                    Object.Destroy(_rotatedSprites[i]);
                    Object.Destroy(_rotatedTextures[i]);
                    _rotatedSprites[i] = null;
                    _rotatedTextures[i] = null;
                }
                else
                {
                    // if no runtime texture, avoid destroying asset references
                    _rotatedSprites[i] = null;
                }
            }
        }
        // keep index 0 (original) untouched
    }

    public int GetSlotWidth()
    {
        return (_rotationIndex % 2 == 0) ? _origSlotW : _origSlotH;
    }

    public int GetSlotHeight()
    {
        return (_rotationIndex % 2 == 0) ? _origSlotH : _origSlotW;
    }
    public void UpdateCountLabel()
    {
        _labelCount.text = m_Stack.quantity == 0 ? "" : m_Stack.quantity.ToString();
    }

    public Vector2 GetOriginalPosition()
    {
        return m_OriginalPosition;
    }
    public void SetOriginalPosition(Vector2 position)
    {
        m_OriginalPosition = position;
    }
    public void SetOriginalRotateIndex(int index)
    {
        m_OriginalRotateIndex = index;
    }
}