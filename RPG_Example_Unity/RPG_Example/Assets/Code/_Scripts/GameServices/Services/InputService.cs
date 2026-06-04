using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputService : MonoBehaviour, IGameServices, IShutdownable
{
    private const string RebindsKey = "input_rebinds_v1";
    public PlayerInputActions Actions { get; private set; }

    public event Action<bool> OnMapOpen;

    private void Awake()
    {
        Actions = new PlayerInputActions();
        LoadRebinds();
        Actions.Enable();

        // UI action maps start disabled — enabled explicitly on demand.
        Actions.UI_MainMenu.Disable();
        Actions.UI_Credits.Disable();
        Actions.UI_TabView.Disable();
        Actions.UI_PauseMenu.Disable();
        Actions.UI_Inventory.Disable();
        Actions.UI_Map.Disable();
        Actions.UI.Disable();
        Actions.UI_Map.Disable();
        Actions.UI_Settings.Disable();
        Actions.UI_PauseSettings.Disable();
    }

    public void Shutdown()
    {
        Actions.Disable();
        Actions.Dispose();
    }

    #region Persistence

    public void SaveRebinds()
    {
        PlayerPrefs.SetString(RebindsKey, Actions.asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public void LoadRebinds()
    {
        if (PlayerPrefs.HasKey(RebindsKey))
            Actions.asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(RebindsKey));
    }

    public void ResetRebinds()
    {
        Actions.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(RebindsKey);
        PlayerPrefs.Save();
    }

    #endregion

    #region Helpers

    public static int FindBindingIndex(InputAction action, string bindingGroup, string compositePartName = null)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (!string.IsNullOrEmpty(bindingGroup) && !b.groups.Contains(bindingGroup)) continue;

            if (string.IsNullOrEmpty(compositePartName))
            {
                if (!b.isPartOfComposite) return i;
            }
            else if (b.isPartOfComposite && b.name == compositePartName)
            {
                return i;
            }
        }
        return -1;
    }

    public string GetDisplayString(string actionPath, string bindingGroup, string compositePartName = null)
    {
        var action = Actions.asset.FindAction(actionPath, true);
        int idx = FindBindingIndex(action, bindingGroup, compositePartName);
        return idx >= 0 ? action.GetBindingDisplayString(idx) : "-";
    }

    public void StartRebind(string actionPath, string bindingGroup, string compositePartName,
        Action onComplete, Action onCancel)
    {
        var action = Actions.asset.FindAction(actionPath, true);
        int idx = FindBindingIndex(action, bindingGroup, compositePartName);
        if (idx < 0) { onCancel?.Invoke(); return; }

        action.Disable();
        var op = action.PerformInteractiveRebinding(idx)
            .WithControlsExcluding("Mouse/position")
            .WithControlsExcluding("Mouse/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .WithMatchingEventsBeingSuppressed();

        op.OnComplete(_ =>
        {
            op.Dispose();
            action.Enable();
            SaveRebinds();
            onComplete?.Invoke();
        });

        op.OnCancel(_ =>
        {
            op.Dispose();
            action.Enable();
            onCancel?.Invoke();
        });

        op.Start();
    }

    #endregion

    // Resets all UI action maps and enables Player.
    // Call at scene load to recover from any previous scene leaving UI maps active.
    public void ResetToGameplayState()
    {
        Actions.UI.Disable();
        Actions.UI_TabView.Disable();
        Actions.UI_Inventory.Disable();
        Actions.UI_PauseMenu.Disable();
        Actions.UI_Map.Disable();
        Actions.UI_Settings.Disable();
        Actions.UI_PauseSettings.Disable();
        Actions.Player.Enable();
    }

    #region Action map transitions

    // ── Generic UI (dialogue, menus) ──────────────────────────────────────────

    public void OnUIOpen()
    {
        Actions.Player.Disable();
        Actions.UI.Enable();
    }

    public void OnUIClose()
    {
        Actions.Player.Enable();
        Actions.UI.Disable();
    }

    // ── TabView (inventory panel + pause menu share the same UI_Inventory map) ─
    // OnUITabViewOpen/Close are called by TabViewManager.
    // The actual sub-map (Inventory vs PauseMenu) is enabled separately below
    // so the correct set of bindings is active for each tab.

    public void OnUITabViewOpen()
    {
        Actions.Player.Disable();
        Actions.UI_TabView.Enable();
    }

    public void OnUITabViewClose()
    {
        Actions.Player.Enable();
        Actions.UI_TabView.Disable();
    }

    // ── Inventory tab ─────────────────────────────────────────────────────────

    public void OnUIInventoryOpen()
    {
        Actions.Player.Disable();
        Actions.UI_PauseMenu.Disable();

        Actions.UI_Inventory.Enable();
    }

    public void OnUIInventoryClose()
    {
        Actions.UI_Inventory.Disable();

        Actions.Player.Enable();
    }

    public void OnUIMapOpen()
    {
        // Move and Sprint stay enabled so the player can walk while the map is open.
        // All combat/interaction actions are disabled individually to prevent ghost inputs.
        OnMapOpen?.Invoke(true);
        Actions.Player.Interact.Disable();
        Actions.Player.OpenMap.Disable();
        Actions.UI_Map.Enable();
    }

    public void OnUIMapClose()
    {
        OnMapOpen?.Invoke(false);
        
        Actions.UI_Map.Disable();
        Actions.Player.Dodge.Enable();
        Actions.Player.LightAttack.Enable();
        Actions.Player.HeavyAttack.Enable();
        Actions.Player.Parry.Enable();
        Actions.Player.LockOn.Enable();
        Actions.Player.LockOnSwap.Enable();
        Actions.Player.Interact.Enable();
        Actions.Player.UseConsumable.Enable();
        Actions.Player.OpenInventory.Enable();
        Actions.Player.OpenMap.Enable();
    }

    public void OnUIControlOpen()
    {
        Actions.UI_Inventory.Disable();
    }
    public void OnUIControlClose()
    {
        Actions.Player.Enable();
    }
    // ── Pause menu tab ────────────────────────────────────────────────────────
    // Pause menu reuses UI_Inventory map — both are inside the same TabView panel.
    // If a dedicated UI_PauseMenu action map is added to the InputActions asset,
    // swap these to enable/disable that map instead.

    public void OnUIPauseMenuOpen()
    {
        Actions.Player.Disable();
        Actions.UI_Inventory.Disable();

        Actions.UI_PauseMenu.Enable();
    }

    public void OnUIPauseMenuClose()
    {
        Actions.Player.Enable();
        Actions.UI_PauseMenu.Disable();
    }

    // ── Settings panel ────────────────────────────────────────────────────────
    // Settings is a sub-panel of the Main Menu — UI_MainMenu stays disabled while
    // settings is open. If a UI_Settings map is added to the asset, swap here.

    public void OnUISettingsOpen()
    {
        Actions.UI_MainMenu.Disable();
        Actions.UI_Settings.Enable();
    }

    public void OnUISettingsClose()
    {
        Actions.UI_Settings.Disable();
        Actions.UI_MainMenu.Enable();
    }

    public void OnUIPauseSettingsOpen()
    {
        Actions.UI_PauseMenu.Disable();
        Actions.UI_PauseSettings.Enable();
        Actions.UI_Settings.Enable();
    }

    public void OnUIPauseSettingsClose()
    {
        Actions.UI_PauseMenu.Enable();
        Actions.UI_PauseSettings.Disable();
        Actions.UI_Settings.Disable();
    }
    // ── Main menu ─────────────────────────────────────────────────────────────

    public void OnUIMainMenuOpen()  => Actions.UI_MainMenu.Enable();
    public void OnUIMainMenuClose() => Actions.UI_MainMenu.Disable();

    // ── Credits panel ─────────────────────────────────────────────────────────

    public void OnUICreditsOpen()
    {
        Actions.UI_MainMenu.Disable();
        Actions.UI_Credits.Enable();
    }

    public void OnUICreditsClose()
    {
        Actions.UI_MainMenu.Enable();
        Actions.UI_Credits.Disable();
    }

    //  ── Map panel ─────────────────────────────────────────────────────────

    public void OnMapPanelOpen()
    {
        Actions.Player.Disable();
        Actions.UI_Map.Enable();
    }

    public void OnMapPanelClose()
    {
        Actions.Player.Enable();
        Actions.UI_Map.Disable();
    }

    #endregion
}