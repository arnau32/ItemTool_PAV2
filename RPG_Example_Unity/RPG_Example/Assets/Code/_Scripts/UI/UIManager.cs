using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public UIDocument uiDocument;
    public VisualTreeAsset dialogueUXML;
    public VisualTreeAsset tabViewUXML;

    public VisualElement root;
    public VisualElement dialoguePanel;

    // inventoryUXML is instantiated here and contains the full TabView (inventory + pause tabs).
    // TabViewManager.m_Root points to tabViewPanel.

    // Alias exposed for TabViewManager — same reference as inventoryPanel.
    // TabViewManager uses this name so both can coexist while we migrate naming.
    public VisualElement tabViewPanel;

    public bool AllUIInitFinish;

    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;

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

        _input = GameServices.Get<InputService>();
        AllUIInitFinish = false;
        NavigationRegistry.Reset();

        root = uiDocument.rootVisualElement;

        dialoguePanel = dialogueUXML.Instantiate();
        ConfigurePanelSize(dialoguePanel);

        tabViewPanel = tabViewUXML.Instantiate();
        ConfigurePanelSize(tabViewPanel);

        // Hide everything while layout stabilizes — restored in Start().
        root.style.opacity = 0f;

        root.Add(tabViewPanel);
        root.Add(dialoguePanel);
    }

    private async void Start()
    {
        await UniTask.WaitUntil(() => PlayerInventory.Instance != null &&
                                      PlayerInventory.Instance.InventoryInit);

        // InventoryInit true → worldBound measured → safe to collapse panels.
        tabViewPanel.style.display = DisplayStyle.None;
        dialoguePanel.style.display = DisplayStyle.None;

        // Restore root opacity — HUD, equipment slots, and all scene UI become visible.
        root.style.opacity = StyleKeyword.Null;

        AllUIInitFinish = true;
        ConfigureInput();
    }

    private void ConfigureInput()
    {
        // ── Dialogue ──────────────────────────────────────────────────────────
        InputActions.UI.Submit.performed   += DialogueManager.Instance.OnSubmit;
        InputActions.UI.Navigate.performed  += DialogueManager.Instance.OnMove;
        InputActions.UI.Cancel.performed    += DialogueManager.Instance.OnCancel;

        // ── TabView open / close ──────────────────────────────────────────────
        // TabViewManager owns open/close and decides which tab to show.
        InputActions.Player.OpenInventory.performed += TabViewManager.Instance.OnOpenInventory;
        InputActions.UI_TabView.CloseTabView.performed += TabViewManager.Instance.OnCloseTabView;
        InputActions.UI_Inventory.OpenInventory.performed += TabViewManager.Instance.OnOpenInventory;

        // ── Inventory tab actions ─────────────────────────────────────────────
        InputActions.UI_Inventory.Move.performed += PlayerInventory.Instance.OnMoveCursor;
        InputActions.UI_Inventory.PickUp.performed += PlayerInventory.Instance.OnPickUpItem;
        InputActions.UI_Inventory.AutoTransferItem.canceled += PlayerInventory.Instance.OnAutoTransferItem;
        InputActions.UI_Inventory.TransferAll.started += PlayerInventory.Instance.OnPress;
        InputActions.UI_Inventory.TransferAll.canceled += PlayerInventory.Instance.OnRelease;
        InputActions.UI_Inventory.Equip.performed += PlayerInventory.Instance.OnToggleEquip;
        InputActions.UI_Inventory.Equip.canceled  += PlayerInventory.Instance.OnToggleEquip;
        InputActions.UI_Inventory.RotateItem.performed += PlayerInventory.Instance.OnRotate;
        InputActions.UI_Inventory.SwitchContainerLeft.performed += PlayerInventory.Instance.OnSwitchContainerLeft;
        InputActions.UI_Inventory.SwitchContainerRight.performed += PlayerInventory.Instance.OnSwitchContainerRight;
        InputActions.UI_Inventory.EquipConsumbaleItem.performed += PlayerInventory.Instance.OnDpadQuickEquip;
        InputActions.UI_Inventory.EquipConsumbaleItem.started  += PlayerInventory.Instance.OnDpadConsumeHoldStarted;
        InputActions.UI_Inventory.EquipConsumbaleItem.canceled += PlayerInventory.Instance.OnDpadConsumeHoldCanceled;

        // ── Tab switching (LB / RB inside the TabView) ────────────────────────
        InputActions.UI_TabView.SwitchTabLeft.performed += TabViewManager.Instance.OnSwitchTabLeft;
        InputActions.UI_TabView.SwitchTabRight.performed += TabViewManager.Instance.OnSwitchTabRight;


        // ── Map Panel ────────────────────────────────────────
        InputActions.Player.OpenMap.performed += MapPanelActive.Instance.OnOpenMapPanel;
        InputActions.UI_Map.CloseMap.performed += MapPanelActive.Instance.OnCloseMapPanel;
    }

    private void OnDestroy()
    {
        if (_input == null) return;

        InputActions.UI.Submit.performed   -= DialogueManager.Instance.OnSubmit;
        InputActions.UI.Navigate.performed  -= DialogueManager.Instance.OnMove;
        InputActions.UI.Cancel.performed    -= DialogueManager.Instance.OnCancel;

        InputActions.Player.OpenInventory.performed -= TabViewManager.Instance.OnOpenInventory;
        InputActions.UI_TabView.CloseTabView.performed -= TabViewManager.Instance.OnCloseTabView;
        InputActions.UI_Inventory.OpenInventory.performed -= TabViewManager.Instance.OnOpenInventory;

        InputActions.UI_Inventory.Move.performed -= PlayerInventory.Instance.OnMoveCursor;
        InputActions.UI_Inventory.PickUp.performed -= PlayerInventory.Instance.OnPickUpItem;
        InputActions.UI_Inventory.AutoTransferItem.canceled -= PlayerInventory.Instance.OnAutoTransferItem;
        InputActions.UI_Inventory.TransferAll.started -= PlayerInventory.Instance.OnPress;
        InputActions.UI_Inventory.TransferAll.canceled -= PlayerInventory.Instance.OnRelease;
        InputActions.UI_Inventory.Equip.performed -= PlayerInventory.Instance.OnToggleEquip;
        InputActions.UI_Inventory.Equip.canceled  -= PlayerInventory.Instance.OnToggleEquip;
        InputActions.UI_Inventory.RotateItem.performed -= PlayerInventory.Instance.OnRotate;
        InputActions.UI_Inventory.SwitchContainerLeft.performed -= PlayerInventory.Instance.OnSwitchContainerLeft;
        InputActions.UI_Inventory.SwitchContainerRight.performed -= PlayerInventory.Instance.OnSwitchContainerRight;
        InputActions.UI_Inventory.EquipConsumbaleItem.performed -= PlayerInventory.Instance.OnDpadQuickEquip;
        InputActions.UI_Inventory.EquipConsumbaleItem.started  -= PlayerInventory.Instance.OnDpadConsumeHoldStarted;
        InputActions.UI_Inventory.EquipConsumbaleItem.canceled -= PlayerInventory.Instance.OnDpadConsumeHoldCanceled;

        InputActions.UI_TabView.SwitchTabLeft.performed -= TabViewManager.Instance.OnSwitchTabLeft;
        InputActions.UI_TabView.SwitchTabRight.performed -= TabViewManager.Instance.OnSwitchTabRight;

        InputActions.Player.OpenMap.performed -= MapPanelActive.Instance.OnOpenMapPanel;
        InputActions.UI_Map.CloseMap.performed -= MapPanelActive.Instance.OnCloseMapPanel;
    }

    private void ConfigurePanelSize(VisualElement panel)
    {
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.top = 0;
        panel.style.right = 0;
        panel.style.bottom = 0;
    }
}