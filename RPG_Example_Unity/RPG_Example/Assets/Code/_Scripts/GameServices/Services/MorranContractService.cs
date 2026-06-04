using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages Morran's random one-attempt contracts.
/// Register this MonoBehaviour in GameBootstrap alongside QuestService.
/// </summary>
public class MorranContractService : MonoBehaviour, IGameServices, IInitializable, ISaveable
{
    #region Fields

    [SerializeField] private MorranContractSO[] _contractPool;

    private MorranContractSO _active;
    private bool _extractionConfirmed;
    private PlayerInventory _subscribedInventory;

    #endregion

    #region Properties

    /// <summary>Fires when a contract is assigned (non-null) or removed (null).</summary>
    public event Action<MorranContractSO> OnContractChanged;

    /// <summary>Fires when item progress changes on the active contract (pickup). Does not imply assignment.</summary>
    public event Action<MorranContractSO> OnContractProgressChanged;

    /// <summary>Fires with the completed contract just before it is cleared.</summary>
    public event Action<MorranContractSO> OnContractCompleted;

    public bool HasActive => _active != null;

    /// <summary>True when the player is carrying every required item for the active contract.</summary>
    public bool CanComplete
    {
        get
        {
            if (_active == null) return false;
            if (_active.requiredItems == null || _active.requiredItems.Length == 0) return false;

            var inventory = PlayerInventory.Instance;
            if (inventory == null || !inventory.InventoryInit) return false;

            foreach (var req in _active.requiredItems)
            {
                int total = 0;
                for (int i = 0; i < inventory.ItemStacks.Count; i++)
                {
                    if (inventory.ItemStacks[i].data == req.item)
                        total += inventory.ItemStacks[i].quantity;
                }
                if (total < req.quantity) return false;
            }
            return true;
        }
    }

    /// <summary>The currently active contract. Null when none is assigned.</summary>
    public MorranContractSO ActiveContract => _active;

    #endregion

    #region IInitializable

    public void Initialize()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);

        SceneManager.sceneLoaded += OnSceneLoaded;
        WaitAndSubscribeInventoryAsync().Forget();
    }

    #endregion

    #region Unity Callbacks

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromInventory();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Picks a random contract from the pool. No-op if a contract is already active.
    /// Avoids repeating the current one when the pool contains more than one entry.
    /// </summary>
    public void AssignRandom()
    {
        if (HasActive) return;

        if (_contractPool == null || _contractPool.Length == 0)
        {
            Debug.LogWarning("[MorranContractService] Contract pool is empty.");
            return;
        }

        var candidates = BuildCandidateList();
        _active = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        OnContractChanged?.Invoke(_active);
        SaveImmediate();
    }

    /// <summary>
    /// Consumes the required items and grants the reward.
    /// No-op if the player does not have all required items.
    /// </summary>
    public void Complete()
    {
        if (!CanComplete) return;

        var contract = _active;
        _active = null;

        OnContractCompleted?.Invoke(contract);
        OnContractChanged?.Invoke(null);
        ConsumeRequiredItems(contract);

        if (GameServices.TryGet<QuestService>(out var qs))
            qs.GrantRewardAsync(contract.reward).Forget();

        SaveImmediate();
    }

    /// <summary>Called by ExtractionZone so the contract survives the trip back to base.</summary>
    public void ReportExtractionSuccess()
    {
        _extractionConfirmed = true;
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        data.morranContract.activeContractId = _active?.contractId ?? "";
    }

    public void ApplyFromSave(SaveData data)
    {
        _extractionConfirmed = false;

        string id = data.morranContract.activeContractId;
        _active = string.IsNullOrEmpty(id) ? null : FindById(id);
    }

    #endregion

    #region Private

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsubscribeFromInventory();
        WaitAndSubscribeInventoryAsync().Forget();
    }

    private async UniTaskVoid WaitAndSubscribeInventoryAsync()
    {
        try
        {
            await UniTask.WaitUntil(() => PlayerInventory.Instance != null, cancellationToken: destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_subscribedInventory != null) return;

        _subscribedInventory = PlayerInventory.Instance;
        _subscribedInventory.OnItemAdded += HandleItemAdded;
    }

    private void UnsubscribeFromInventory()
    {
        if (_subscribedInventory == null) return;
        _subscribedInventory.OnItemAdded -= HandleItemAdded;
        _subscribedInventory = null;
    }

    private void HandleItemAdded(ItemStack stack)
    {
        if (_active == null || _active.requiredItems == null) return;

        for (int i = 0; i < _active.requiredItems.Length; i++)
        {
            if (_active.requiredItems[i].item == stack.data)
            {
                OnContractProgressChanged?.Invoke(_active);
                return;
            }
        }
    }

    private List<MorranContractSO> BuildCandidateList()
    {
        var list = new List<MorranContractSO>(_contractPool.Length);

        for (int i = 0; i < _contractPool.Length; i++)
        {
            if (_contractPool[i] != null && _contractPool[i] != _active)
                list.Add(_contractPool[i]);
        }

        if (list.Count == 0)
        {
            for (int i = 0; i < _contractPool.Length; i++)
            {
                if (_contractPool[i] != null) list.Add(_contractPool[i]);
            }
        }

        return list;
    }

    private void ConsumeRequiredItems(MorranContractSO contract)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        foreach (var req in contract.requiredItems)
        {
            int remaining = req.quantity;

            for (int i = inventory.ItemStacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = inventory.ItemStacks[i];
                if (stack.data != req.item) continue;

                int take = Mathf.Min(stack.quantity, remaining);
                stack.Remove(take);
                remaining -= take;

                if (stack.IsEmpty)
                    inventory.RemoveItemStack(stack);
                else
                    stack.RootVisual?.UpdateCountLabel();
            }
        }
    }

    private MorranContractSO FindById(string id)
    {
        for (int i = 0; i < _contractPool.Length; i++)
        {
            if (_contractPool[i] != null && _contractPool[i].contractId == id)
                return _contractPool[i];
        }
        return null;
    }

    private static void SaveImmediate()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            save.SaveImmediate();
    }

    #endregion
}
