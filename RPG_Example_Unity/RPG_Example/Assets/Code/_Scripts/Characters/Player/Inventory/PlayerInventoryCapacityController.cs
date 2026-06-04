using UnityEngine;
using System;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class PlayerInventoryCapacityController : MonoBehaviour, ISaveable
{
    public static PlayerInventoryCapacityController Instance;

    private int currentLevel = 0;

    [Header("Initial Size")]
    public int startWidth = 4;
    public int startHeight = 2;

    [Header("Max Size")]
    public int maxWidth = 6;
    public int maxHeight = 4;

    [System.Serializable]
    public struct InventoryUpgradeStep
    {
        public int width;
        public int height;
    }

    [Header("Upgrade Steps")]
    public List<InventoryUpgradeStep> upgradeSteps = new()
    {
        new InventoryUpgradeStep { width = 4, height = 2 },
        new InventoryUpgradeStep { width = 5, height = 2 },
        new InventoryUpgradeStep { width = 6, height = 2 },
        new InventoryUpgradeStep { width = 6, height = 3 },
        new InventoryUpgradeStep { width = 6, height = 4 },
    };

    public int CurrentWidth  { get; private set; }
    public int CurrentHeight { get; private set; }

    private SaveService _save;

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        CurrentWidth  = startWidth;
        CurrentHeight = startHeight;

        if (GameServices.TryGet(out _save))
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

    private void Start()
    {
        ApplyCurrentLevel();
    }

    #endregion

    #region Public API

    public void InitInventoryGridCapacity()
    {
        var grid = PlayerInventory.Instance.GetInventoryGrid();
        if (grid != null) grid.Clear();

        CreateGrid(CurrentWidth, CurrentHeight);

        PlayerInventory.Instance.InventoryDimensions = new Dimensions
        {
            Width  = CurrentWidth,
            Height = CurrentHeight
        };
    }

    public bool UpgradeCapacity()
    {
        if (currentLevel >= upgradeSteps.Count - 1)
            return false;

        currentLevel++;

        var step      = upgradeSteps[currentLevel];
        CurrentWidth  = step.width;
        CurrentHeight = step.height;

        HandleCapacityChanged(CurrentWidth, CurrentHeight);

        return true;
    }

    public void CreateGrid(int width, int height)
    {
        if (PlayerInventory.Instance.GetInventoryGrid() == null)
            return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("slotIcon");
                PlayerInventory.Instance.GetInventoryGrid().Add(slot);
            }
        }

        PlayerInventory.Instance.GetInventoryGrid().style.width = width * 75;
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        data.inventory.inventoryCapacityLevel = currentLevel;
    }

    public void ApplyFromSave(SaveData data)
    {
        int saved    = data.inventory.inventoryCapacityLevel;
        int newLevel = Mathf.Clamp(saved, 0, upgradeSteps.Count - 1);

        var step     = upgradeSteps[newLevel];
        CurrentWidth  = step.width;
        CurrentHeight = step.height;

        // If the level actually changed and the inventory UI is already built
        // (player persists across scenes), rebuild the grid immediately.
        if (newLevel != currentLevel)
        {
            currentLevel = newLevel;
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.IsInventoryReady)
                HandleCapacityChanged(CurrentWidth, CurrentHeight);
        }
        else
        {
            currentLevel = newLevel;
        }
    }

    #endregion

    #region Private

    private void ApplyCurrentLevel()
    {
        var step      = upgradeSteps[currentLevel];
        CurrentWidth  = step.width;
        CurrentHeight = step.height;
    }

    private async void HandleCapacityChanged(int w, int h)
    {
        var grid = PlayerInventory.Instance.GetInventoryGrid();
        if (grid != null)
            grid.Clear();

        CreateGrid(w, h);

        PlayerInventory.Instance.InventoryDimensions = new Dimensions
        {
            Width  = w,
            Height = h
        };

        await PlayerInventory.Instance.RebuildInventoryUI();

        StartCoroutine(RebuildNavNextFrame());
    }

    private IEnumerator RebuildNavNextFrame()
    {
        yield return null;
        PlayerInventory.Instance.CacheGridOrigin();
        NavigationRegistry.RebuildNodes();
        PlayerInventory.Instance.ResetCursorToInventoryStart();
    }

    #endregion
}
