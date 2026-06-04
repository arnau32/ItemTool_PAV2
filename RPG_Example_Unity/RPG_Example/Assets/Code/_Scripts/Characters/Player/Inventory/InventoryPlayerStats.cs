using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryPlayerStats : MonoBehaviour
{
    #region Fields

    private static readonly Enums.EquipSlot[] ALL_SLOTS =
    {
        Enums.EquipSlot.Backpack, Enums.EquipSlot.Helmet,
        Enums.EquipSlot.ChestArmor, Enums.EquipSlot.Weapon, Enums.EquipSlot.LowerArmor,
    };

    [SerializeField] private CharacterHealthSystem _health;
    [SerializeField] private StaminaSystem _stamina;

    private ProgressBar _healthBar;
    private Label       _healthLabel;
    private ProgressBar _staminaBar;
    private Label       _staminaLabel;
    private Label       _defenseLabel;
    private Label       _attackLabel;

    // Ghost bar: shows past health value that decays to current after damage.
    // Preview bar: shows potential heal when hovering a consumable.
    // Both are VisualElements injected into the ProgressBar's background container.
    private VisualElement _healthGhostEl;
    private VisualElement _healthPreviewEl;

    private float _ghostValue01;
    private float _ghostLastHealth01;
    private float _ghostTimer;
    private float _previewValue01;

    private const float GHOST_DELAY = 0.15f;
    private const float GHOST_SPEED = 4f;
    private const float EPSILON     = 0.001f;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        var root = UIManager.Instance.tabViewPanel;
        root.Q<VisualElement>("StatsBox").parent.style.display = DisplayStyle.Flex;

        var healthBox  = root.Q<VisualElement>("HealthBox");
        var staminaBox = root.Q<VisualElement>("StaminaBox");
        var defenseBox = root.Q<VisualElement>("Defense");
        var attackBox  = root.Q<VisualElement>("Attack");

        _healthBar    = healthBox.Q<ProgressBar>();
        _healthLabel  = healthBox.Q<Label>("HealthText");
        _staminaBar   = staminaBox.Q<ProgressBar>();
        _staminaLabel = staminaBox.Q<Label>("StaminaText");
        _defenseLabel = defenseBox.Q<Label>("DefenseText");
        _attackLabel  = attackBox.Q<Label>("AttackText");

        BuildHealthGhostBars();

        _ghostValue01      = _health.CurrentHealth01;
        _ghostLastHealth01 = _health.CurrentHealth01;

        _health.OnHealthChanged    += RefreshHealth;
        _health.OnMaxHealthChanged += RefreshHealth;
        _stamina.OnStaminaModifies += RefreshStamina;

        EquipmentSlotContainer.OnEquipmentChanged += OnEquipmentChanged;
        ItemDescription.OnHoveredStackChanged     += OnHoveredStackChanged;

        RefreshHealth(_health.CurrentHealth01);
        RefreshStamina();
        RefreshCombatStats(null);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnHealthChanged    -= RefreshHealth;
            _health.OnMaxHealthChanged -= RefreshHealth;
        }

        if (_stamina != null)
            _stamina.OnStaminaModifies -= RefreshStamina;

        EquipmentSlotContainer.OnEquipmentChanged -= OnEquipmentChanged;
        ItemDescription.OnHoveredStackChanged     -= OnHoveredStackChanged;
    }

    private void Update()
    {
        if (_health == null || _healthGhostEl == null) return;

        float current01 = _health.CurrentHealth01;

        if (Mathf.Abs(_ghostValue01 - current01) > EPSILON)
        {
            _ghostTimer += Time.deltaTime;
            if (_ghostTimer >= GHOST_DELAY)
                _ghostValue01 = Mathf.MoveTowards(_ghostValue01, current01, GHOST_SPEED * Time.deltaTime);
        }
        else
        {
            _ghostValue01 = current01;
            _ghostTimer   = 0f;
        }

        UpdateGhostVisual(current01);
    }

    #endregion

    #region Private — Health Ghost Bars

    private void BuildHealthGhostBars()
    {
        if (_healthBar == null) return;

        var bg = _healthBar.Q<VisualElement>(className: "unity-progress-bar__background");
        if (bg == null) return;

        _healthGhostEl = new VisualElement { name = "health-ghost" };
        _healthGhostEl.style.position        = Position.Absolute;
        _healthGhostEl.style.top             = new StyleLength(0f);
        _healthGhostEl.style.bottom          = new StyleLength(0f);
        _healthGhostEl.style.backgroundColor = new Color(0.75f, 0.20f, 0.20f, 0.55f);
        _healthGhostEl.style.display         = DisplayStyle.None;
        bg.Add(_healthGhostEl);

        _healthPreviewEl = new VisualElement { name = "health-preview" };
        _healthPreviewEl.style.position        = Position.Absolute;
        _healthPreviewEl.style.top             = new StyleLength(0f);
        _healthPreviewEl.style.bottom          = new StyleLength(0f);
        _healthPreviewEl.style.backgroundColor = new Color(0.20f, 0.72f, 0.20f, 0.60f);
        _healthPreviewEl.style.display         = DisplayStyle.None;
        bg.Add(_healthPreviewEl);
    }

    private void UpdateGhostVisual(float current01)
    {
        if (_healthGhostEl == null) return;

        bool show = _ghostValue01 > current01 + EPSILON;
        _healthGhostEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        if (show)
        {
            _healthGhostEl.style.left  = new StyleLength(new Length(current01 * 100f, LengthUnit.Percent));
            _healthGhostEl.style.width = new StyleLength(new Length((_ghostValue01 - current01) * 100f, LengthUnit.Percent));
        }
    }

    private void UpdatePreviewVisual(float current01)
    {
        if (_healthPreviewEl == null) return;

        bool show = _previewValue01 > EPSILON;
        _healthPreviewEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        if (show)
        {
            float clampedPreview = Mathf.Min(_previewValue01, 1f - current01);
            _healthPreviewEl.style.left  = new StyleLength(new Length(current01 * 100f, LengthUnit.Percent));
            _healthPreviewEl.style.width = new StyleLength(new Length(clampedPreview * 100f, LengthUnit.Percent));
        }
    }

    #endregion

    #region Private — Stats

    private void RefreshHealth(float normalized)
    {
        _healthBar.value  = normalized * 100f;
        _healthLabel.text = $"{Mathf.CeilToInt(_health.CurrentHealth)} / {Mathf.CeilToInt(_health.MaxHealth)}";

        float delta = normalized - _ghostLastHealth01;

        if (delta < -EPSILON)
        {
            _ghostTimer = 0f;
        }
        else if (delta > EPSILON)
        {
            // On heal: snap ghost to current so no ghost is visible
            _ghostValue01 = normalized;
            _ghostTimer   = 0f;
        }

        _ghostLastHealth01 = normalized;
        UpdatePreviewVisual(normalized);
    }

    private void RefreshStamina()
    {
        _staminaBar.value  = _stamina.CurrentStamina01 * 100f;
        _staminaLabel.text = $"{Mathf.CeilToInt(_stamina.CurrentStamina)} / {Mathf.CeilToInt(_stamina.MaxStamina)}";
    }

    private void OnEquipmentChanged()                    => RefreshCombatStats(ItemDescription.CurrentHoveredStack);
    private void OnHoveredStackChanged(ItemStack stack)  => RefreshCombatStats(stack);

    private void RefreshCombatStats(ItemStack hovered)
    {
        float totalDef = GetTotalEquippedStat(Enums.StatType.Defense);
        float totalAtk = GetTotalEquippedStat(Enums.StatType.Attack);

        float deltaDef = GetHoveredDelta(hovered, Enums.StatType.Defense);
        float deltaAtk = GetHoveredDelta(hovered, Enums.StatType.Attack);

        if (_defenseLabel != null) _defenseLabel.text = FormatStatLabel(totalDef, deltaDef);
        if (_attackLabel  != null) _attackLabel.text  = FormatStatLabel(totalAtk, deltaAtk);

        RefreshHealPreview(hovered);
    }

    private void RefreshHealPreview(ItemStack stack)
    {
        if (stack?.data is ConsumableItemData consumable && _health != null)
        {
            float totalHeal = CalculateTotalHeal(consumable);
            float max       = _health.MaxHealth;
            _previewValue01 = max > 0f ? Mathf.Clamp01(totalHeal / max) : 0f;
        }
        else
        {
            _previewValue01 = 0f;
        }

        UpdatePreviewVisual(_health != null ? _health.CurrentHealth01 : 0f);
    }

    // InstantHeal: total = baseValue
    // HealOverTime: total = baseValue (per tick) × duration (ticks)
    private static float CalculateTotalHeal(ConsumableItemData consumable)
    {
        if (consumable.buffs == null) return 0f;

        float total = 0f;
        for (int i = 0; i < consumable.buffs.Count; i++)
        {
            var buff = consumable.buffs[i];
            switch (buff.applicationMode)
            {
                case Enums.BuffApplicationMode.InstantHeal:
                    total += buff.baseValue;
                    break;
                case Enums.BuffApplicationMode.HealOverTime:
                    total += buff.baseValue * buff.duration;
                    break;
            }
        }
        return total;
    }

    private static float GetTotalEquippedStat(Enums.StatType statType)
    {
        float total = 0f;
        for (int i = 0; i < ALL_SLOTS.Length; i++)
        {
            var slot = ContainerRegistry.GetEquipmentSlot(ALL_SLOTS[i]);
            if (slot?.EquippedStack == null) continue;
            var mods = slot.EquippedStack.GetEffectiveModifiers();
            if (mods == null) continue;
            for (int j = 0; j < mods.Count; j++)
            {
                var m = mods[j];
                if (m.statTypeAffected == statType && m.type == StatModifierType.Flat)
                    total += m.value;
            }
        }
        return total;
    }

    private static float GetHoveredDelta(ItemStack hovered, Enums.StatType statType)
    {
        if (hovered?.data is not EquipableItemData equipData) return 0f;

        var equippedSlot = ContainerRegistry.GetEquipmentSlot(equipData.equipSlot);
        if (equippedSlot?.EquippedStack == hovered) return 0f;

        var hoveredMods = hovered.GetEffectiveModifiers();
        if (hoveredMods == null) return 0f;

        var equippedMods = equippedSlot?.EquippedStack?.GetEffectiveModifiers();

        float hoveredSum  = SumStatFlat(hoveredMods, statType);
        float equippedSum = equippedMods != null ? SumStatFlat(equippedMods, statType) : 0f;
        return hoveredSum - equippedSum;
    }

    private static float SumStatFlat(List<StatModifier> mods, Enums.StatType statType)
    {
        float sum = 0f;
        for (int i = 0; i < mods.Count; i++)
        {
            var m = mods[i];
            if (m.statTypeAffected == statType && m.type == StatModifierType.Flat)
                sum += m.value;
        }
        return sum;
    }

    private static string FormatStatLabel(float baseValue, float delta)
    {
        string baseStr = ((int)baseValue).ToString();
        if (Mathf.Approximately(delta, 0f)) return baseStr;

        string sign  = delta > 0f ? "+" : string.Empty;
        string color = delta > 0f ? "#33DD33" : "#F24444";
        return $"{baseStr} <color={color}>({sign}{(int)delta})</color>";
    }

    #endregion
}