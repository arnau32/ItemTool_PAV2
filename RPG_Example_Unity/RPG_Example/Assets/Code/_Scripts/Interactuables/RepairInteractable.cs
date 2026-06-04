using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct RepairRequirement
{
    public ItemData itemData;
    public int quantity;
}

public class RepairInteractable : BaseInteractable, ISaveable
{
    #region Fields

    [Header("Identity")]
    [SerializeField] private string _repairId;

    [Header("Requirements")]
    [SerializeField] private RepairRequirement[] _requirements;

    [Header("Quest Gate (optional)")]
    [Tooltip("If set, this repair also requires the quest to be InProgress or CanFinish.")]
    [SerializeField] private string _requiredQuestId;

    [Header("State Objects")]
    [SerializeField] private GameObject _brokenObjects;

    [SerializeField] private GameObject _repairedObjects;

    [Header("Proximity UI")]
    [SerializeField] private Collider _proximityTrigger;

    [SerializeField] private CanvasGroup _requirementsPanel;
    [SerializeField] private Transform _slotsContainer;
    [SerializeField] private RepairSlotUI _slotPrefab;

    [Header("Events")]
    [SerializeField] private UnityEvent _onInteractFailed;

    private bool _isRepaired;
    private RepairSlotUI[] _spawnedSlots;
    private Collider _detectionCollider;
    private InteractableTarget _interactableTarget;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _detectionCollider = GetComponent<Collider>();
        _interactableTarget = GetComponent<InteractableTarget>();

        /*
         * Important:
         * Both states start enabled so Unity has both objects initialized in scene.
         * After that, Start() or ApplyFromSave() decides which one is visible.
         */
        SetBothStateObjectsActive(true);

        if (!string.IsNullOrEmpty(_repairId) && GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);

        InitializeSlots();
        HideRequirementsPanel();
    }

    private void Start()
    {
        /*
         * Default runtime state.
         * If save data has already been applied, _isRepaired will already be true.
         * If save data is applied later, ApplyFromSave() will override this again.
         */
        ApplyVisualState();
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(_repairId) && GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isRepaired) return;

        RefreshSlotAvailability();
        ShowRequirementsPanel();
    }

    private void OnTriggerExit(Collider other)
    {
        HideRequirementsPanel();
    }

    #endregion

    #region IInteractable

    public override void OnInteract()
    {
        if (_isRepaired) return;

        if (!HasRequiredItems())
        {
            _onInteractFailed?.Invoke();
            return;
        }

        base.OnInteract();

        ConsumeItems();
        ApplyRepair(saveState: true);

        if (!string.IsNullOrEmpty(_repairId) && GameServices.TryGet<QuestService>(out var qs))
            qs.ReportInteraction(_repairId);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_repairId)) return;

        data.repairables.SetRepaired(_repairId, _isRepaired);
    }

    public void ApplyFromSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_repairId)) return;

        _isRepaired = data.repairables.IsRepaired(_repairId);
        ApplyVisualState();
    }

    #endregion

    #region Private

    private void InitializeSlots()
    {
        if (_slotPrefab == null || _slotsContainer == null || _requirements == null || _requirements.Length == 0)
            return;

        _spawnedSlots = new RepairSlotUI[_requirements.Length];

        for (int i = 0; i < _requirements.Length; i++)
        {
            var slot = Instantiate(_slotPrefab, _slotsContainer);
            var req = _requirements[i];

            if (req.itemData != null && slot.Icon != null)
                slot.Icon.sprite = req.itemData.icon;

            if (slot.CountText != null)
                slot.CountText.text = $"x{req.quantity}";

            _spawnedSlots[i] = slot;
        }
    }

    private void RefreshSlotAvailability()
    {
        if (_spawnedSlots == null) return;

        var inv = PlayerInventory.Instance;

        for (int i = 0; i < _spawnedSlots.Length; i++)
        {
            bool available = inv != null
                             && CountInInventory(inv, _requirements[i].itemData) >= _requirements[i].quantity;

            _spawnedSlots[i].SetAvailable(available);
        }
    }

    private void ShowRequirementsPanel()
    {
        if (_requirementsPanel == null) return;

        _requirementsPanel.gameObject.SetActive(true);
        _requirementsPanel.alpha = 1f;
    }

    private void HideRequirementsPanel()
    {
        if (_requirementsPanel == null) return;

        _requirementsPanel.gameObject.SetActive(false);
    }

    private bool HasRequiredItems()
    {
        if (!string.IsNullOrEmpty(_requiredQuestId))
        {
            if (!GameServices.TryGet<QuestService>(out var qs)) return false;
            var quest = qs.GetQuestById(_requiredQuestId);
            if (quest == null) return false;
            if (quest.state is not (Enums.QuestState.InProgress or Enums.QuestState.CanFinish))
                return false;
        }

        var inv = PlayerInventory.Instance;
        if (inv == null) return false;

        for (int i = 0; i < _requirements.Length; i++)
        {
            if (CountInInventory(inv, _requirements[i].itemData) < _requirements[i].quantity)
                return false;
        }

        return true;
    }

    private int CountInInventory(PlayerInventory inv, ItemData data)
    {
        if (data == null) return 0;

        int count = 0;
        var stacks = inv.ItemStacks;

        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data != null && stacks[i].data.uniqueID == data.uniqueID)
                count += stacks[i].quantity;
        }

        if (data is CollectableItemData cd && cd.isAuroraDust)
            count += inv.AuraDust;

        return count;
    }

    private void ConsumeItems()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        for (int i = 0; i < _requirements.Length; i++)
        {
            if (_requirements[i].itemData == null) continue;

            int remaining = _requirements[i].quantity;
            var stacks = inv.ItemStacks;
            var toRemove = new List<ItemStack>();

            for (int j = stacks.Count - 1; j >= 0 && remaining > 0; j--)
            {
                if (stacks[j].data == null || stacks[j].data.uniqueID != _requirements[i].itemData.uniqueID)
                    continue;

                int take = Mathf.Min(remaining, stacks[j].quantity);

                stacks[j].quantity -= take;
                remaining -= take;

                if (stacks[j].quantity <= 0)
                    toRemove.Add(stacks[j]);
                else
                    stacks[j].RootVisual?.UpdateCountLabel();
            }

            for (int j = 0; j < toRemove.Count; j++)
                inv.RemoveItemStack(toRemove[j]);

            if (remaining > 0 && _requirements[i].itemData is CollectableItemData cd && cd.isAuroraDust)
                inv.TrySpend(remaining);
        }
    }

    private void ApplyRepair(bool saveState)
    {
        _isRepaired = true;

        ApplyVisualState();

        if (!saveState) return;

        if (GameServices.TryGet<SaveService>(out var save))
            save.Save();
    }

    private void ApplyVisualState()
    {
        if (_isRepaired)
        {
            SetBrokenState(false);
            SetRepairedState(true);

            if (_interactableTarget != null)
            {
                _interactableTarget.SetHighlighted(false);
                _interactableTarget.enabled = false;
            }

            if (_detectionCollider != null)
                _detectionCollider.enabled = false;

            if (_proximityTrigger != null)
                _proximityTrigger.enabled = false;

            HideRequirementsPanel();
        }
        else
        {
            SetBrokenState(true);
            SetRepairedState(false);

            if (_interactableTarget != null)
                _interactableTarget.enabled = true;

            if (_detectionCollider != null)
                _detectionCollider.enabled = true;

            if (_proximityTrigger != null)
                _proximityTrigger.enabled = true;
        }
    }

    private void SetBothStateObjectsActive(bool active)
    {
        SetBrokenState(active);
        SetRepairedState(active);
    }

    private void SetBrokenState(bool active)
    {
        if (_brokenObjects != null)
            _brokenObjects.SetActive(active);
    }

    private void SetRepairedState(bool active)
    {
        if (_repairedObjects != null)
            _repairedObjects.SetActive(active);
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_repairId))
            Debug.LogWarning($"[RepairInteractable] '{name}' has no _repairId. State will not be saved.", this);

        if (_proximityTrigger == null)
            Debug.LogWarning($"[RepairInteractable] '{name}' has no _proximityTrigger assigned. Collider will keep firing after repair.", this);

        if (_brokenObjects == null)
            Debug.LogWarning($"[RepairInteractable] '{name}' has no _brokenObjects assigned.", this);

        if (_repairedObjects == null)
            Debug.LogWarning($"[RepairInteractable] '{name}' has no _repairedObjects assigned.", this);

        if (_requirements != null)
        {
            for (int i = 0; i < _requirements.Length; i++)
            {
                if (_requirements[i].itemData == null)
                    Debug.LogWarning($"[RepairInteractable] '{name}': requirement [{i}] has no ItemData.", this);

                if (_requirements[i].quantity <= 0)
                    Debug.LogWarning($"[RepairInteractable] '{name}': requirement [{i}] quantity is <= 0.", this);
            }
        }
    }
#endif
}