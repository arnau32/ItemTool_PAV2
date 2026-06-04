using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

// Displays contextual input hints in the inventory panel.
public class InputIconInventory : MonoBehaviour
{
    private IconDatabase      iconDatabase;
    private InputIconSettings _inputIcons;

    [SerializeField] private Loots _loots;

    private const int MAX_HINTS = 5;

    private VisualElement      _root;
    private ActionHintElement[] _pool;
    private HintState           _lastState;

    private void Start()
    {
        _inputIcons  = GameServices.Get<InputIconSettings>();
        _root        = UIManager.Instance.tabViewPanel.Q<VisualElement>("InputInstructions");
        iconDatabase = _inputIcons.CurrentIconDatabase;

        _inputIcons.OnInputDeviceChanged += OnDeviceChanged;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        BuildPool();
        _lastState = default;
    }

    private void OnDestroy()
    {
        if (_inputIcons != null)
            _inputIcons.OnInputDeviceChanged -= OnDeviceChanged;

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnDeviceChanged()
    {
        iconDatabase = _inputIcons.CurrentIconDatabase;

        // ǿ��ˢ�� UI
        _lastState = default;
    }

    private void Update()
    {
        var newState = ComputeState();

        if (newState.Equals(_lastState)) return;

        _lastState = newState;
        ApplyStateToPool(newState);
    }

    /// Forces an immediate hint refresh bypassing the dirty check.
    /// Called by InventoryCursorManager.UpdateInfo() when cursor position changes.
    public void UpdateInputActionIcon()
    {
        _lastState = default;
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        _lastState = default; 
    }

    #region State Computation

    private HintState ComputeState()
    {
        var state = new HintState();

        state.Add(InputIconType.LB, InputIconType.RB, "Input_SwitchPanel");

        if (ContainerRegistry._currentlyDragging == null)
        {
            var container = InventoryCursorManager.Instance.currentContainer;
            if (container == null) return state;

            bool lootActive = _loots != null && _loots.isActive;

            switch (container)
            {
                case PlayerInventory:
                    if(lootActive)
                    {
                        state.Add(InputIconType.ButtonNorth, "Input_HOLDStorageAll");
                    }

                    break;

                case Loots:
                    state.Add(InputIconType.ButtonNorth, "Input_HOLDCollect All");
                    break;
            }

                    var stack = container.GetStackAt(
                InventoryCursorManager.Instance.gridX,
                InventoryCursorManager.Instance.gridY);
            if (stack == null) return state;

            state.Add(InputIconType.ButtonSouth, "Input_PickUp");

            

            switch (container)
            {
                case PlayerInventory:
                    if (stack.data.itemType == Enums.ItemType.Equipable)
                        state.Add(InputIconType.ButtonWest, "Input_Equip");
                    else if (stack.data.itemType == Enums.ItemType.Consumable)
                    {
                        state.Add(InputIconType.ButtonWest, "Input_Use");
                    }

                    state.Add(InputIconType.ButtonNorth, lootActive ? "Input_Storage" : "Input_Discard");
                    break;

                case Loots:
                    if (stack.data.itemType == Enums.ItemType.Equipable)
                        state.Add(InputIconType.ButtonWest, "Input_Equip");
                    else if (stack.data.itemType == Enums.ItemType.Consumable)
                    {
                        state.Add(InputIconType.ButtonWest, "Input_Use");
                    }

                    state.Add(InputIconType.ButtonNorth, "Input_Collect");
                    break;

                case EquipmentSlotContainer:
                case ConsumableSlotContainer:
                    state.Add(InputIconType.ButtonWest, "Input_Use");
                    state.Add(InputIconType.ButtonNorth, lootActive ? "Input_Storage" : "Input_Discard");
                    break;
            }
        }
        else
        {
            state.Add(InputIconType.ButtonSouth, "Input_PutDown");
            state.Add(InputIconType.RightJS_CLK, "Input_Rotate");
        }

        return state;
    }

    #endregion

    #region Pool

    private void BuildPool()
    {
        _pool = new ActionHintElement[MAX_HINTS];

        for (int i = 0; i < MAX_HINTS; i++)
        {
            _pool[i] = new ActionHintElement(null, null, string.Empty);
            _pool[i].SetVisible(false);
            _root.Add(_pool[i]);
        }
    }

    private void ApplyStateToPool(HintState state)
    {
        for (int i = 0; i < MAX_HINTS; i++)
        {
            if (i < state.count)
            {
                var entry = state.entries[i];
                _pool[i].UpdateContent(
                    iconDatabase.Get(entry.iconA),
                    entry.hasTwoIcons ? iconDatabase.Get(entry.iconB) : null,
                    Loc.Get(entry.label));
                _pool[i].SetVisible(true);
            }
            else
            {
                _pool[i].SetVisible(false);
            }
        }
    }

    #endregion

    #region Inner Types

    private struct HintEntry
    {
        public InputIconType iconA;
        public InputIconType iconB;
        public string label;
        public bool hasTwoIcons;
    }

    private struct HintState
    {
        public HintEntry e0, e1, e2, e3, e4;
        public int count;

        public HintEntry[] entries => new[] { e0, e1, e2, e3, e4 };

        public void Add(InputIconType a, string label)
        {
            var entry = new HintEntry { iconA = a, label = label, hasTwoIcons = false };
            SetAt(count, entry);
            count++;
        }

        public void Add(InputIconType a, InputIconType b, string label)
        {
            var entry = new HintEntry { iconA = a, iconB = b, label = label, hasTwoIcons = true };
            SetAt(count, entry);
            count++;
        }

        private void SetAt(int i, HintEntry e)
        {
            switch (i)
            {
                case 0: e0 = e; break;
                case 1: e1 = e; break;
                case 2: e2 = e; break;
                case 3: e3 = e; break;
                case 4: e4 = e; break;
            }
        }

        public bool Equals(HintState other)
        {
            if (count != other.count) return false;
            return EntryEq(e0, other.e0) && EntryEq(e1, other.e1) &&
                   EntryEq(e2, other.e2) && EntryEq(e3, other.e3) &&
                   EntryEq(e4, other.e4);
        }

        private static bool EntryEq(HintEntry a, HintEntry b) =>
            a.iconA == b.iconA && a.iconB == b.iconB &&
            a.hasTwoIcons == b.hasTwoIcons && a.label == b.label;
    }

    #endregion
}

public static class Loc
{
    private const string TABLE = "UI_Inventory";

    public static string Get(string key)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(TABLE, key);
    }
}