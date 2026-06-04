using System.Collections.Generic;
using UnityEngine;
using FeedbacksNagu;

public enum StorageType
{
    Bag,
    Chest
}

public class Storage : BaseInteractable, IAnimatedInteractable
{
    [Header("Storage Identity")] [Tooltip("If true, this storage's contents are saved and restored between sessions.")] [SerializeField]
    private bool _isPersistent = false;

    [Tooltip("Unique persistence key. snake_case — 'stash_main', 'chest_crafting_01'.")] [SerializeField]
    private string _storageId;

    [Tooltip("Stacks que tendra este contenedor al iniciarse / al generarse por loot.")]
    public List<ItemStack> items = new();

    public FeedbackContainer onInteractFeedback;
    [SerializeField] private FeedbackContainer _onCloseFeedback;
    public StorageType storageType;

    [Header("Quest")] public bool canFinishQuest = false;
    public string interactionId;

    [Header("Chest Playable Action")] [Tooltip("Assign a ChestOpenPlayableAction SO. PlayerInteractable routes through it before calling OnInteract.")] [SerializeField]
    private ChestOpenPlayableAction _openAction;

    #region Unity Callbacks

    private void Awake()
    {
        if (!_isPersistent) return;

        if (!ValidateId()) return;

        if (!GameServices.TryGet<StorageSaveService>(out var storageSave)) return;

        storageSave.RegisterStorage(_storageId, items);
    }

    private void OnDestroy()
    {
        if (!_isPersistent) return;

        if (GameServices.TryGet<StorageSaveService>(out var storageSave))
            storageSave.UnregisterStorage(_storageId);
    }

    #endregion

    #region IAnimatedInteractable

    /// <summary>
    /// Called by PlayerInteractable. Injects self into the action SO and returns it.
    /// Storage never references the player — the action SO owns the completion callback.
    /// </summary>
    public PlayerPlayableAction GetPlayableAction()
    {
        if (_openAction == null)
        {
            Debug.LogWarning($"[Storage] '{name}' has no ChestOpenPlayableAction assigned. Falling back to instant open.", this);
            OnInteract();
            return null;
        }

        _openAction.PrepareForStorage(this);
        return _openAction;
    }

    #endregion

    #region Public API

    // Called by ChestOpenPlayableAction.OnCompleted — Storage stays unaware of the player.
    public override void OnInteract()
    {
        base.OnInteract();

        items ??= new List<ItemStack>();

        onInteractFeedback.PlayFeedbacks(gameObject);
        TabViewManager.Instance.OpenLoots(items, storageType);
        TabViewManager.Instance.OnTabClose += HandleStorageClosed;

        if (string.IsNullOrEmpty(interactionId)) return;

        if (!GameServices.TryGet<QuestService>(out var qs)) return;
        qs.ReportInteraction(interactionId);
        if (canFinishQuest)
            qs.FinishQuest(interactionId);
    }

    private void HandleStorageClosed()
    {
        TabViewManager.Instance.OnTabClose -= HandleStorageClosed;
        _onCloseFeedback.PlayFeedbacks(gameObject);
    }

    public void AddItem(ItemStack stack)
    {
        items ??= new List<ItemStack>();

        if (stack != null && stack.data != null && stack.quantity > 0)
            items.Add(stack);
    }

    public void AddItems(List<ItemStack> stacks)
    {
        if (stacks == null || stacks.Count == 0) return;

        items ??= new List<ItemStack>();

        for (int i = 0; i < stacks.Count; i++)
        {
            var s = stacks[i];
            if (s != null && s.data != null && s.quantity > 0)
                items.Add(s);
        }
    }

    #endregion

    #region Helpers

    private bool ValidateId()
    {
        return !string.IsNullOrEmpty(_storageId);
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!_isPersistent) return;

        if (string.IsNullOrEmpty(_storageId))
            Debug.LogWarning($"[Storage] '{name}' is persistent but _storageId is empty. " +
                             "Set a unique snake_case ID (e.g. 'stash_main').", this);
    }
#endif
}