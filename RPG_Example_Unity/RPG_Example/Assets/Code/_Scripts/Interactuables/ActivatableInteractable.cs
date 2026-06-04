using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ActivatableInteractable : BaseInteractable, ISaveable
{
    #region Fields

    [Header("Identity")]
    [SerializeField] private string _activatableId;

    [Header("Quest Lock")]
    [SerializeField] private string _requiredQuestId;

    [Header("Requirements")]
    [SerializeField] private RepairRequirement[] _requirements;

    [Header("State Objects")]
    [SerializeField] private GameObject _inactiveObjects;
    [SerializeField] private GameObject _activeObjects;

    [Header("Proximity UI")]
    [SerializeField] private Collider _proximityTrigger;
    [SerializeField] private CanvasGroup _requirementsPanel;
    [SerializeField] private Transform _slotsContainer;
    [SerializeField] private RepairSlotUI _slotPrefab;

    [Header("Events")]
    [SerializeField] private UnityEvent _onActivated;
    [SerializeField] private UnityEvent _onInteractFailed;

    private bool _isActivated;
    private bool _isLockedByQuest;
    private RepairSlotUI[] _spawnedSlots;
    private Collider _detectionCollider;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _detectionCollider = GetComponent<Collider>();

        if (!string.IsNullOrEmpty(_activatableId) && GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);

        if (!string.IsNullOrEmpty(_requiredQuestId) && GameServices.TryGet<QuestService>(out var qs))
            qs.OnQuestStateChanged += HandleQuestStateChanged;

        InitializeSlots();
        HideRequirementsPanel();
    }

    private void Start()
    {
        RefreshQuestLockState();
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(_activatableId) && GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(this);

        if (!string.IsNullOrEmpty(_requiredQuestId) && GameServices.TryGet<QuestService>(out var qs))
            qs.OnQuestStateChanged -= HandleQuestStateChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
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
        if (_isActivated)
        {
            base.OnInteract();
            return;
        }

        if (!HasRequiredItems())
        {
            _onInteractFailed?.Invoke();
            return;
        }

        ConsumeItems();
        Activate(saveState: true);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_activatableId)) return;
        data.repairables.SetRepaired(_activatableId, _isActivated);
    }

    public void ApplyFromSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_activatableId)) return;
        if (data.repairables.IsRepaired(_activatableId))
            Activate(saveState: false);
    }

    #endregion

    #region Private

    private void RefreshQuestLockState()
    {
        if (string.IsNullOrEmpty(_requiredQuestId)) return;
        if (!GameServices.TryGet<QuestService>(out var qs)) return;

        if (qs.IsQuestFinished(_requiredQuestId))
        {
            _isLockedByQuest = false;
            qs.OnQuestStateChanged -= HandleQuestStateChanged;
            return;
        }

        _isLockedByQuest = true;
        if (_detectionCollider != null) _detectionCollider.enabled = false;
        if (_proximityTrigger != null) _proximityTrigger.enabled = false;
    }

    private void HandleQuestStateChanged(Quest quest)
    {
        if (!_isLockedByQuest) return;
        if (!GameServices.TryGet<QuestService>(out var qs)) return;
        if (!qs.IsQuestFinished(_requiredQuestId)) return;

        _isLockedByQuest = false;
        qs.OnQuestStateChanged -= HandleQuestStateChanged;

        if (_isActivated) return;

        if (_detectionCollider != null) _detectionCollider.enabled = true;
        if (_proximityTrigger != null) _proximityTrigger.enabled = true;
    }

    private void Activate(bool saveState)
    {
        _isActivated = true;

        if (_detectionCollider != null) _detectionCollider.enabled = false;
        if (_proximityTrigger != null) _proximityTrigger.enabled = false;
        HideRequirementsPanel();

        if (_inactiveObjects != null) _inactiveObjects.SetActive(false);
        if (_activeObjects != null) _activeObjects.SetActive(true);

        if (!saveState) return;

        _onActivated?.Invoke();

        if (!string.IsNullOrEmpty(_activatableId) && GameServices.TryGet<QuestService>(out var qs))
            qs.ReportInteraction(_activatableId);

        if (GameServices.TryGet<SaveService>(out var save))
            save.Save();
    }

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

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_activatableId))
            Debug.LogWarning($"[ActivatableInteractable] '{name}' has no _activatableId. State will not be saved.", this);

        if (_proximityTrigger == null)
            Debug.LogWarning($"[ActivatableInteractable] '{name}' has no _proximityTrigger assigned. Collider will keep firing after activation.", this);
    }
#endif
}
