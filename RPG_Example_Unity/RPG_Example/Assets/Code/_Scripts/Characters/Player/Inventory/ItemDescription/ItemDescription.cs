using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using static Enums;

public class ItemDescription : MonoBehaviour
{
    #region Fields
    [SerializeField] private StatModifierDescriptionConfig _statConfig;
    [SerializeField] private Sprite _tierIcon;

    private VisualElement m_Root;
    private VisualElement m_DescriptionRoot;
    private VisualElement m_ItemIcon;
    private VisualElement m_ItemIconPanel;
    private Label m_ItemName;
    private Label m_ItemType;
    private VisualElement m_DescriptionBox;
    private Label m_ItemRarity;
    private Label m_ItemValue;

    private ItemStack _lastDescribedStack;
    private ItemStack _lastEquippedStack;
    private LocalizedString _lastLocalizedName;
    private LocalizedString _lastLocalizedRarity;

    public LocalizedString _itemTypeConsumable;
    public LocalizedString _itemTypeCollectable;
    public LocalizedString _itemTypeCrafting;
    public LocalizedString _itemTypeBackpack;

    [Header("Weapon Families")]
    public LocalizedString _weaponFamilyHands;
    public LocalizedString _weaponFamilySword;
    public LocalizedString _weaponFamilyGreatSword;
    public LocalizedString _weaponFamilyWarrior;
    public LocalizedString _weaponFamilySpear;
    #endregion

    #region Properties
    public static ItemDescription Instance { get; private set; }
    public static event System.Action<ItemStack> OnHoveredStackChanged;
    public static ItemStack CurrentHoveredStack => Instance?._lastDescribedStack;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(this);
    }

    private void Start()
    {
        m_Root = UIManager.Instance.tabViewPanel;
        m_DescriptionRoot = m_Root.Q<VisualElement>("Description");

        m_ItemName = m_DescriptionRoot.Q<Label>("ItemName");
        m_ItemType = m_DescriptionRoot.Q<Label>("ItemTypeText");
        m_ItemIcon = m_DescriptionRoot.Q<VisualElement>("ItemIcon");
        m_ItemIconPanel = m_DescriptionRoot.Q<VisualElement>("ItemIconPanel");
        m_DescriptionBox = m_DescriptionRoot.Q<VisualElement>("DescriptionBox");
        m_ItemRarity = m_DescriptionRoot.Q<Label>("RarityText");
        m_ItemValue = m_DescriptionRoot.Q<Label>("ItemValue");
    }

    private void Update()
    {
        UpdateDescription();
    }
    #endregion

    #region Public API
    public void UpdateDescription()
    {
        if (TryActivePanel())
        {
            m_DescriptionRoot.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (_lastDescribedStack != null)
            {
                _lastDescribedStack = null;
                OnHoveredStackChanged?.Invoke(null);
            }
            m_DescriptionRoot.style.display = DisplayStyle.None;
        }
    }
    #endregion

    #region Private Methods
    private void OnItemNameChanged(string value) => m_ItemName.text = value;
    private void OnRarityNameChanged(string value) => m_ItemRarity.text = value;

    private bool TryActivePanel()
    {
        var container = InventoryCursorManager.Instance.currentContainer;
        if (container == null) return false;

        var stack = container.GetStackAt(InventoryCursorManager.Instance.gridX, InventoryCursorManager.Instance.gridY);
        if (stack == null) return false;

        if (stack.RootVisual == ContainerRegistry._currentlyDragging) return false;

        m_ItemIcon.style.backgroundImage = stack.data.icon != null ? stack.data.icon.texture : null;
        var effectiveRarity = stack.GetEffectiveRarity();

        m_ItemIconPanel.style.unityBackgroundImageTintColor = RarityColorProvider.RarityColorConfig.GetColor(effectiveRarity);

        switch (stack.data.itemType)
        {
            case ItemType.Equipable:
                EquipableItemData equipableData = stack.data as EquipableItemData;
                switch (equipableData.equipSlot)
                {
                    case EquipSlot.Weapon:
                        WeaponData weaponData = stack.data as WeaponData;
                        switch (weaponData.familyType)
                        {
                            case WeaponFamily.Hands:      m_ItemType.text = _weaponFamilyHands.GetLocalizedString();      break;
                            case WeaponFamily.Sword:      m_ItemType.text = _weaponFamilySword.GetLocalizedString();      break;
                            case WeaponFamily.GreatSword: m_ItemType.text = _weaponFamilyGreatSword.GetLocalizedString(); break;
                            case WeaponFamily.Warrior:    m_ItemType.text = _weaponFamilyWarrior.GetLocalizedString();    break;
                            case WeaponFamily.Spear:      m_ItemType.text = _weaponFamilySpear.GetLocalizedString();      break;
                        }
                        break;
                    case EquipSlot.Backpack:
                        m_ItemType.text = _itemTypeBackpack.GetLocalizedString();
                        break;
                }
                break;
            case ItemType.Consumable:  m_ItemType.text = _itemTypeConsumable.GetLocalizedString();  break;
            case ItemType.Collectable: m_ItemType.text = _itemTypeCollectable.GetLocalizedString(); break;
            case ItemType.Crafting:    m_ItemType.text = _itemTypeCrafting.GetLocalizedString();     break;
        }

        m_ItemRarity.style.color = RarityColorProvider.RarityTextColorConfig.GetColor(effectiveRarity);
        m_ItemValue.text = stack.data.value.ToString();

        // Resolve the currently equipped item for comparison (null if cursor is already on it)
        var equippedForComparison = GetEquippedStackForComparison(stack);

        if (_lastDescribedStack != stack || _lastEquippedStack != equippedForComparison)
        {
            _lastDescribedStack    = stack;
            _lastEquippedStack     = equippedForComparison;
            OnHoveredStackChanged?.Invoke(stack);

            if (_lastLocalizedName != null)
                _lastLocalizedName.StringChanged -= OnItemNameChanged;
            _lastLocalizedName = stack.data.localizedDisplayName;

            if (!_lastLocalizedName.IsEmpty)
            {
                _lastLocalizedName.StringChanged += OnItemNameChanged;
                m_ItemName.text = _lastLocalizedName.GetLocalizedString();
            }
            else
            {
                m_ItemName.text = stack.data.itemNameID;
            }

            if (_lastLocalizedRarity != null)
                _lastLocalizedRarity.StringChanged -= OnRarityNameChanged;
            _lastLocalizedRarity = RarityColorProvider.RarityTextColorConfig.GetLocalizedName(effectiveRarity);

            if (_lastLocalizedRarity != null && !_lastLocalizedRarity.IsEmpty)
            {
                _lastLocalizedRarity.StringChanged += OnRarityNameChanged;
                m_ItemRarity.text = _lastLocalizedRarity.GetLocalizedString();
            }
            else
            {
                m_ItemRarity.text = effectiveRarity.ToString();
            }

            GenerateDescriptionBlock(stack, equippedForComparison);
        }

        return true;
    }

    // Returns the equipped stack for the same slot as stack, or null if stack IS the equipped item.
    private static ItemStack GetEquippedStackForComparison(ItemStack stack)
    {
        if (stack?.data is not EquipableItemData equipableData) return null;

        var slot = ContainerRegistry.GetEquipmentSlot(equipableData.equipSlot);
        if (slot == null) return null;

        var equippedStack = slot.EquippedStack;

        // No comparison when hovering over the equipped item itself
        if (equippedStack == null || equippedStack == stack) return null;

        return equippedStack;
    }

    private void GenerateDescriptionBlock(ItemStack stack, ItemStack equippedStack)
    {
        m_DescriptionBox.Clear();

        if (stack.data is EquipableItemData equipable)
        {
            if (equipable.tier > 0)
                m_DescriptionBox.Add(BuildTierRow(equipable.tier));

            var modifiers = stack.GetEffectiveModifiers();
            if (modifiers == null) return;

            var equippedModifiers = equippedStack?.GetEffectiveModifiers();

            // Track (statType, modifierType) pairs already shown with a delta to avoid duplicates.
            // HashSet<int> with a packed key avoids tuple naming issues with the Unity compiler.
            var shownDeltaKeys = new HashSet<int>();

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                float? delta  = null;

                if (equippedModifiers != null)
                {
                    int key = ((int)modifier.statTypeAffected << 8) | (int)modifier.type;

                    if (shownDeltaKeys.Add(key))
                    {
                        float hoveredSum  = SumModifiers(modifiers,        (int)modifier.statTypeAffected, modifier.type);
                        float equippedSum = SumModifiers(equippedModifiers, (int)modifier.statTypeAffected, modifier.type);
                        float d           = hoveredSum - equippedSum;

                        if (d != 0f) delta = d;
                    }
                }

                m_DescriptionBox.Add(BuildWeaponDescriptionRow(modifier, delta));
            }
        }
        else
        {
            foreach (var block in stack.data.descriptionBlocks)
                m_DescriptionBox.Add(block.CreateWrappedElement());
        }
    }

    private VisualElement BuildTierRow(int tier)
    {
        var row = new VisualElement();
        row.name = "TierDescription";
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.alignItems = Align.Center;
        row.style.alignSelf = Align.Stretch;
        row.style.height = 40;
        row.style.marginLeft = 24;
        row.style.flexShrink = 0;

        var bg = new VisualElement();
        bg.style.position = Position.Absolute;
        bg.style.top = 5;
        bg.style.right = 5;
        bg.style.bottom = 5;
        bg.style.left = 5;
        bg.style.backgroundColor = new Color(0f, 0f, 0f, 0.31f);
        row.Add(bg);

        var icon = new VisualElement();
        icon.name = "StatsIcon";
        icon.style.height = 28;
        icon.style.aspectRatio = 1f;
        icon.style.flexShrink = 0;
        icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        if (_tierIcon != null)
            icon.style.backgroundImage = new StyleBackground(_tierIcon);
        row.Add(icon);

        var valueLabel = new Label(tier.ToString());
        valueLabel.name = "StatsNumber";
        valueLabel.AddToClassList("desc-text");
        valueLabel.style.fontSize = 40;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        valueLabel.style.height = 30;
        valueLabel.style.marginTop = 0;
        valueLabel.style.marginBottom = 0;
        valueLabel.style.marginLeft = 0;
        valueLabel.style.marginRight = 0;
        valueLabel.style.paddingTop = 0;
        valueLabel.style.paddingBottom = 0;
        valueLabel.style.paddingLeft = 0;
        valueLabel.style.paddingRight = 0;
        row.Add(valueLabel);

        var nameLabel = new Label("Tier");
        nameLabel.name = "StatsText";
        nameLabel.AddToClassList("desc-text");
        nameLabel.style.fontSize = 25;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.height = 24;
        row.Add(nameLabel);

        return row;
    }

    private static float SumModifiers(List<StatModifier> mods, int statTypeInt, StatModifierType modType)
    {
        float sum = 0f;
        for (int i = 0; i < mods.Count; i++)
        {
            if ((int)mods[i].statTypeAffected == statTypeInt && mods[i].type == modType)
                sum += mods[i].value;
        }
        return sum;
    }

    private VisualElement BuildWeaponDescriptionRow(StatModifier modifier, float? delta = null)
    {
        var row = new VisualElement();
        row.name = "WeaponDescription";
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.alignItems = Align.Center;
        row.style.alignSelf = Align.Stretch;
        row.style.height = 40;
        row.style.marginLeft = 24;
        row.style.flexShrink = 0;

        var bg = new VisualElement();
        bg.style.position = Position.Absolute;
        bg.style.top = 5;
        bg.style.right = 5;
        bg.style.bottom = 5;
        bg.style.left = 5;
        bg.style.backgroundColor = new Color(0f, 0f, 0f, 0.31f);
        row.Add(bg);

        var icon = new VisualElement();
        icon.name = "StatsIcon";
        icon.style.height = 28;
        icon.style.aspectRatio = 1f;
        icon.style.flexShrink = 0;
        icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

        var nameLabel = new Label();
        nameLabel.name = "StatsText";
        nameLabel.AddToClassList("desc-text");
        nameLabel.style.fontSize = 25;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.height = 24;

        if (_statConfig != null && _statConfig.TryGetEntry(modifier.statTypeAffected, out var statEntry))
        {
            if (statEntry.icon != null)
                icon.style.backgroundImage = new StyleBackground(statEntry.icon);
            else if (!string.IsNullOrEmpty(statEntry.iconClass))
                icon.AddToClassList(statEntry.iconClass);

            nameLabel.text = statEntry.localizedName.GetLocalizedString();
            statEntry.localizedName.StringChanged += v => nameLabel.text = v;
        }
        else
        {
            nameLabel.text = modifier.statTypeAffected.ToString();
        }

        row.Add(icon);
        row.Add(BuildValueLabel(modifier));
        row.Add(nameLabel);

        if (delta.HasValue)
            row.Add(BuildDeltaLabel(delta.Value, modifier.type));

        return row;
    }

    private Label BuildValueLabel(StatModifier modifier)
    {
        float val = modifier.value;
        string sign = val >= 0f ? "+" : string.Empty;
        string text = modifier.type == StatModifierType.Flat
            ? $"{sign}{val:0.#}"
            : $"{sign}{val * 100f:0.#}%";

        var label = new Label(text);
        label.name = "StatsNumber";
        label.AddToClassList("desc-text");
        label.style.fontSize = 40;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.height = 30;
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        label.style.marginLeft = 0;
        label.style.marginRight = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        return label;
    }

    private static Label BuildDeltaLabel(float delta, StatModifierType modType)
    {
        string sign = delta > 0f ? "+" : string.Empty;
        string text = modType == StatModifierType.Flat
            ? $"({sign}{delta:0.#})"
            : $"({sign}{delta * 100f:0.#}%)";

        var label = new Label(text);
        label.name = "StatsDelta";
        label.AddToClassList("desc-text");
        label.style.fontSize = 28;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.height = 30;
        label.style.marginLeft = 6;
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        label.style.marginRight = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        label.style.color = delta > 0f
            ? new Color(0.20f, 0.85f, 0.20f)
            : new Color(0.95f, 0.25f, 0.25f);
        return label;
    }
    #endregion
}