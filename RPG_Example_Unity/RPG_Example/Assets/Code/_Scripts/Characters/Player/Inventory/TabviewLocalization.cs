using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public class TabviewLocalization : MonoBehaviour
{
    public static TabviewLocalization Instance;


    [Header("Tabs")]
    [SerializeField] private Tab _tabInventory;
    [SerializeField] private Tab _tabMenu;

    [SerializeField] private Label _headerInventory;
    [SerializeField] private Label _headerLoots;

    [SerializeField] private Button _continueButton; 
    [SerializeField] private Button _settingButtonButton;
    [SerializeField] private Button _exitButtonButton;

    [Header("Localization")]
    public LocalizedString inventoryText;
    public LocalizedString menuText;

    public LocalizedString headerInventoryText;

    public LocalizedString headerLootsBagText;
    public LocalizedString headerLootsChestText;

    public LocalizedString continueButtonText;
    public LocalizedString settingButtonText;
    public LocalizedString exitButtonText;

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
    }

    public void Init()
    {
        _tabInventory = UIManager.Instance.root.Q<Tab>("Tab_Inventory");
        _tabMenu = UIManager.Instance.root.Q<Tab>("Tab_Menu");
        _headerInventory = UIManager.Instance.root.Q<Label>("Header_Inventory");
        _headerLoots = UIManager.Instance.root.Q<Label>("LootName");

        _continueButton = UIManager.Instance.root.Q<Button>("ContinueButton");
        _settingButtonButton = UIManager.Instance.root.Q<Button>("SettingButton");
        _exitButtonButton = UIManager.Instance.root.Q<Button>("ExitButton");

        RefreshTexts();


        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        RefreshTexts();
    }

    public void RefreshTexts()
    {
        _tabInventory.label = inventoryText.GetLocalizedString();
        _tabMenu.label = menuText.GetLocalizedString();

        _headerInventory.text = headerInventoryText.GetLocalizedString();

        switch (PlayerInventory.Instance.loots._type)
        {
            case StorageType.Bag:
                _headerLoots.text = headerLootsBagText.GetLocalizedString();
                break;
            case StorageType.Chest:
                _headerLoots.text = headerLootsChestText.GetLocalizedString();
                break;
            default:
                _headerLoots.text = headerLootsBagText.GetLocalizedString();
                break;
        }

        _continueButton.text = continueButtonText.GetLocalizedString();
        _settingButtonButton.text = settingButtonText.GetLocalizedString();
        _exitButtonButton.text = exitButtonText.GetLocalizedString();
    }
}

