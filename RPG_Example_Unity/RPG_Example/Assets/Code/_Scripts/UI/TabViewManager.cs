using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public enum TabType
{
    Inventory,
    Menu,
}
public class TabViewManager : MonoBehaviour
{
    public static TabViewManager Instance;

    private VisualElement m_Root;
    public TabView m_TabView;

    private VisualElement ContainerMenu;

    private List<Tab> _tabs;
    private Dictionary<TabType, Tab> _tabMap;
    
    public Action<TabType> TabOpen;
    public Action OnTabClose;

    public TabType m_TabType;

    public GameObject settingPanel;

    public bool isActive = false;

    private SettingsTabs _settingsTabs;

    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }

        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    private void OnDisable()
    {
        // Scene is ending — reset input maps so the next scene starts clean.
        if (!GameServices.TryGet<InputService>(out var input)) return;
        input.ResetToGameplayState();
    }

    void Start()
    {
        m_Root = UIManager.Instance.tabViewPanel;
        m_TabView = m_Root.Q<TabView>("m_TabView");
        ContainerMenu = m_Root.Q<VisualElement>("Container_Menu");

        _tabs = m_TabView.Query<Tab>().ToList();
        isActive = false;
        _tabMap = new Dictionary<TabType, Tab>
    {
        { TabType.Inventory, _tabs[0] },
        { TabType.Menu, _tabs[1] },
    };
        _settingsTabs = settingPanel.GetComponent<SettingsTabs>();
        GameServices.TryGet(out _audio);
        TabViewInputIcon.Instance.Init();
        TabviewLocalization.Instance.Init();
    }

    public void OnOpenInventory(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        CloseMapIfOpen();

        GameServices.Get<InputService>().OnUITabViewOpen();
        OpenTab(TabType.Inventory);
        PlayerInventory.Instance.InitInventory();
    }

    public void OnOpenPauseMenu(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        CloseMapIfOpen();

        GameServices.Get<InputService>().OnUITabViewOpen();
        OpenTab(TabType.Menu);
    }

    public void OnCloseTabView(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (m_TabType == TabType.Menu && _settingsTabs.isActive)
        {
            DesActiveSettingPanel();
            return;
        }
        CloseTabView();
    }

    public void CloseTabView()
    {
        if (m_TabType == TabType.Menu && _settingsTabs.isActive)
        {
            m_Root.style.opacity = 1;
            PauseMenu.Instance._PauseMenuPanelRoot.style.display = DisplayStyle.Flex;
            settingPanel.SetActive(false);
            _settingsTabs.isActive = false;
            GameServices.Get<InputService>().OnUIPauseSettingsClose();
        }

        GameServices.Get<InputService>().OnUITabViewClose();

        switch (m_TabType)
        {
            case TabType.Menu:
                GameServices.Get<InputService>().OnUIPauseMenuClose();
                break;
            case TabType.Inventory:
                PlayerInventory.Instance.CloseInventory();
                GameServices.Get<InputService>().OnUIInventoryClose();
                break;
        }

        isActive = false;

        ConsumableItemSlotManager.Instance.RefreshAllSlots();
        UIManager.Instance.tabViewPanel.style.display = DisplayStyle.None;
        OnTabClose?.Invoke();
    }
    public void OpenLoots(List<ItemStack> sourceItems, StorageType type)
    {
        CloseMapIfOpen();

        GameServices.Get<InputService>().OnUITabViewOpen();
        OpenTab(TabType.Inventory);
        PlayerInventory.Instance.OpenLoots(sourceItems, type);
    }
    public void OpenTab(TabType type)
    {
        if (_tabMap == null || !_tabMap.ContainsKey(type))
            return;

        isActive = true;
        UIManager.Instance.tabViewPanel.style.display = DisplayStyle.Flex;

        Tab targetTab = _tabMap[type];

        m_TabView.activeTab = targetTab;
        m_TabType = type;

        TabSwitchAction();
    }

    public void OnSwitchTabLeft(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (_settingsTabs.isActive)
        {
            return;
        }
        SwitchTab(-1);
    }

    public void OnSwitchTabRight(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (_settingsTabs.isActive)
        {
            return;
        }
        SwitchTab(+1);
    }

    private void SwitchTab(int direction)
    {
        if (_tabs == null || _tabs.Count == 0)
            return;

        int currentIndex = _tabs.IndexOf(m_TabView.activeTab);

        if (currentIndex < 0)
            currentIndex = 0;

        int newIndex = (currentIndex + direction + _tabs.Count) % _tabs.Count;

        Tab newTab = _tabs[newIndex];

        m_TabView.activeTab = newTab;

        foreach (var pair in _tabMap)
        {
            if (pair.Value == newTab)
            {
                m_TabType = pair.Key;
                break;
            }
        }

        PlayTabSwitchSound();
        TabSwitchAction();
    }

    private void PlayTabSwitchSound()
    {
        if (_audio == null) GameServices.TryGet(out _audio);
        if (_audio == null || _uiSounds == null || _uiSounds.MenuNavigate.IsNull) return;
        _audio.PlayOneShot(_uiSounds.MenuNavigate, Vector3.zero);
    }

    private void TabSwitchAction()
    {
        switch (m_TabType)
        {
            case TabType.Menu:
                PauseMenu.Instance.InitPauseMenu();
                GameServices.Get<InputService>().OnUIPauseMenuOpen();
                break;
            case TabType.Inventory:
                GameServices.Get<InputService>().OnUIInventoryOpen();
                break;
        }
        TabOpen.Invoke(m_TabType);
    }

    private void CloseMapIfOpen()
    {
        if (WorldMapController.Instance == null || !WorldMapController.Instance.IsOpen)
            return;

        WorldMapController.Instance.SetMapOpen(false);
        GameServices.Get<InputService>().OnUIMapClose();
    }

    public void ActiveSettingPanel()
    {
        PauseMenu.Instance._PauseMenuPanelRoot.style.display = DisplayStyle.None;
        m_Root.style.opacity = 0;
        _settingsTabs.isActive = true;
        settingPanel.SetActive(true);
    }
    public void DesActiveSettingPanel()
    {
        if (m_Root == null) return;

        PauseMenu.Instance._PauseMenuPanelRoot.style.display = DisplayStyle.Flex;

        m_Root.style.opacity = 1;
        settingPanel.SetActive(false);

        if (_settingsTabs.isActive)
            GameServices.Get<InputService>().OnUIPauseSettingsClose();

        _settingsTabs.isActive = false;
    }
}
