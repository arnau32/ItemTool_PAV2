using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Central quest service. Registered in GameBootstrap as a persistent service.
public class QuestService : MonoBehaviour, IGameServices, IInitializable, IShutdownable, ISaveable
{
    #region Fields

    // Fired on any quest state change. Observed by QuestHUD, QuestIcon, etc.
    public event Action<Quest> OnQuestStateChanged;

    public event Action<List<RewardEntry>> OnRewardsGranted;

    // Fired when a quest step is completed, BEFORE the quest advances to the next step.
    public event Action<Quest, QuestStepSO, bool> OnStepCompleted;

    private readonly Dictionary<string, Quest> _questMap = new(32);
    private readonly List<Quest> _activeQuests = new(8);

    // Quest IDs whose current step completed during a world run but whose
    // advance is deferred until ReportExtractionSuccess() is called.
    // On death (Village load without extraction), this set is discarded.
    private readonly HashSet<string> _pendingAdvances = new(8);

    // All interaction IDs reported this scene load, used by InteractMultipleStepSO
    // to retroactively count NPCs talked to before the quest was accepted.
    private readonly HashSet<string> _reportedInteractions = new(16);

    private bool _extractionConfirmed;

    private PlayerInventory _subscribedInventory;
    private Gameplay.Enemies.EnemyManager _subscribedEnemyManager;

    #endregion

    #region Properties

    // True while the player is in the Village (safe zone).
    // QuestStepSO.Complete() uses this to decide whether to advance
    // the quest immediately or register a pending advance.
    public bool IsInSafeZone { get; private set; }

    #endregion

    #region Public API

    public void Initialize()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);

        BuildQuestMap();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Replace per-frame Update() poll: check requirements only when a
        // quest finishes, which is the only moment new prereq chains unlock.
        OnQuestStateChanged += HandleQuestStateChangedForRequirements;

        WaitAndSubscribeSceneReportersAsync().Forget();
    }

    public void Shutdown()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        OnQuestStateChanged -= HandleQuestStateChangedForRequirements;
        UnsubscribeFromSceneEvents();
    }

    // Called by QuestStepSO.Complete() when in safe zone — advances immediately.
    public void AdvanceQuest(string id)
    {
        if (!TryGetQuest(id, out var quest)) return;

        QuestStepSO completedStep = quest.GetCurrentStepSO();
        bool isLastStep = !quest.HasNextStep();

        Debug.Log($"[Quest] '{id}' step '{completedStep?.name}' completed (last={isLastStep}, safeZone={IsInSafeZone}).");

        OnStepCompleted?.Invoke(quest, completedStep, isLastStep);

        quest.MoveToNextStep();

        if (quest.CurrentStepExists())
        {
            quest.ActivateCurrentStep();

            if (IsInSafeZone)
                quest.CheckCurrentStepCompletion();

            OnQuestStateChanged?.Invoke(quest);
        }
        else
        {
            _activeQuests.Remove(quest);
            SetState(quest, Enums.QuestState.CanFinish);
        }
    }

    // Called by QuestStepSO.Complete() when NOT in safe zone.
    // Quest stays InProgress in memory; advance is confirmed on extraction.
    public void RegisterPendingAdvance(string questId)
    {
        Debug.Log($"[Quest] '{questId}' step completed in world — pending extraction to confirm.");
        _pendingAdvances.Add(questId);

        // Notify observers (e.g. EnemyQuestMarker) so they can hide immediately
        // even though the quest won't advance until extraction is confirmed.
        if (_questMap.TryGetValue(questId, out var quest))
            OnQuestStateChanged?.Invoke(quest);
    }

    // Called by ExtractionZone.StartExtraction() before loading Village.
    // Confirms all pending step completions so the save captures CanFinish.
    // dustPenalty: dust consumed by the extraction fee (door only). Aurora Dust
    // collect steps whose post-penalty total falls below the requirement are NOT
    // advanced — CheckCompletionOnActivate will re-evaluate them in the village.
    public void ReportExtractionSuccess(int dustPenalty = 0)
    {
        _extractionConfirmed = true;

        if (_pendingAdvances.Count > 0)
            Debug.Log($"[Quest] Extraction confirmed — advancing {_pendingAdvances.Count} pending quest(s).");

        foreach (var questId in _pendingAdvances)
        {
            if (ShouldAdvanceAfterExtraction(questId, dustPenalty))
                AdvanceQuest(questId);
            else
                Debug.Log($"[Quest] '{questId}' advance deferred — post-penalty dust below requirement.");
        }

        _pendingAdvances.Clear();
    }

    private bool ShouldAdvanceAfterExtraction(string questId, int dustPenalty)
    {
        if (dustPenalty == 0) return true;
        if (!TryGetQuest(questId, out var quest)) return false;

        var step = quest.GetCurrentStepSO();
        if (step is not CollectItemStepSO collect) return true;
        if (collect.targetItem is not CollectableItemData dustData || !dustData.isAuroraDust) return true;

        var inv = PlayerInventory.Instance;
        if (inv == null) return false;

        int total = inv.AuraDust;
        var stacks = inv.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data is CollectableItemData cd && cd.isAuroraDust)
                total += stacks[i].quantity;
        }

        return (total - dustPenalty) >= collect.requiredQuantity;
    }

    // Start a quest. Called from StartQuestAction or NPC_QuestGiver.
    public void StartQuest(string id)
    {
        if (!TryGetQuest(id, out var quest)) return;

        if (quest.state != Enums.QuestState.CanStart)
        {
            Debug.LogWarning($"[QuestService] StartQuest '{id}': state is {quest.state}, expected CanStart.");
            return;
        }

        Debug.Log($"[Quest] '{id}' started.");
        ActivateQuest(quest);
        SetState(quest, Enums.QuestState.InProgress);

        if (IsInSafeZone)
            quest.CheckCurrentStepCompletion();
    }

    // Finish the quest and grant rewards. Called from FinishQuestAction or NPC_QuestGiver.
    public void FinishQuest(string id, bool consumeItems = false)
    {
        if (!TryGetQuest(id, out var quest)) return;

        if (quest.state != Enums.QuestState.CanFinish)
        {
            Debug.LogWarning($"[QuestService] FinishQuest '{id}': state is {quest.state}, expected CanFinish.");
            return;
        }

        if (!HasRequiredItemsForFinish(quest))
        {
            Debug.LogWarning($"[QuestService] FinishQuest '{id}': items no longer sufficient — reverting to InProgress.");
            RevertQuestToInProgress(quest);
            return;
        }

        Debug.Log($"[Quest] '{id}' finished.");

        if (consumeItems)
            ConsumeQuestItems(quest);

        GrantRewardsAsync(quest).Forget();
        SetState(quest, Enums.QuestState.Finished);
    }

    /// <summary>
    /// Returns true when all CollectItem steps for this quest have their required
    /// items present in the player's inventory right now.
    /// DeliverItem steps are excluded: their items are consumed at the delivery zone.
    /// </summary>
    public bool HasRequiredItemsForFinish(string id)
    {
        return TryGetQuest(id, out var quest) && HasRequiredItemsForFinish(quest);
    }

    private bool HasRequiredItemsForFinish(Quest quest)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || !inv.InventoryInit) return false;

        var steps = quest.info.questSteps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is CollectItemStepSO collect)
            {
                if (!HasEnoughInInventory(inv, collect.targetItem, collect.requiredQuantity))
                    return false;
            }
            else if (steps[i] is DeliverItemStepSO deliver && deliver.consumeOnDelivery)
            {
                if (!HasEnoughInInventory(inv, deliver.requiredItem, deliver.requiredQuantity))
                    return false;
            }
        }
        return true;
    }

    private static bool HasEnoughInInventory(PlayerInventory inv, ItemData item, int required)
    {
        if (item == null) return false;

        int total = 0;
        var stacks = inv.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data != null && stacks[i].data.uniqueID == item.uniqueID)
                total += stacks[i].quantity;
        }

        if (item is CollectableItemData dustData && dustData.isAuroraDust)
            total += inv.AuraDust;

        return total >= required;
    }

    private void ConsumeQuestItems(Quest quest)
    {
        var steps = quest.info.questSteps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is CollectItemStepSO collect)
                ConsumeFromInventory(collect.targetItem, collect.requiredQuantity);
            else if (steps[i] is DeliverItemStepSO deliver)
                ConsumeFromInventory(deliver.requiredItem, deliver.requiredQuantity);
        }
    }

    private static void ConsumeFromInventory(ItemData item, int amount)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || item == null) return;

        var stacks    = inv.ItemStacks;
        int remaining = amount;

        for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var s = stacks[i];
            if (s.data == null || s.data.uniqueID != item.uniqueID) continue;

            int toRemove = Mathf.Min(remaining, s.quantity);
            s.quantity  -= toRemove;
            remaining   -= toRemove;

            if (s.quantity <= 0)
                inv.RemoveItemStack(s);
            else
                s.RootVisual?.UpdateCountLabel();
        }

        if (remaining > 0 && item is CollectableItemData dustData && dustData.isAuroraDust)
            inv.TrySpend(remaining);
    }

    // Called by QuestStepSO.PushStatus() to update HUD text.
    public void NotifyStepStateChanged(string id, int stepIndex, QuestStepState stepState)
    {
        if (!TryGetQuest(id, out var quest)) return;

        quest.StoreStepState(stepState, stepIndex);
        OnQuestStateChanged?.Invoke(quest);
    }

    // Returns all quests. Used by QuestHUD on Start() to refresh display.
    public IReadOnlyCollection<Quest> GetAllQuests() => _questMap.Values;

    public Quest GetQuestById(string id)
    {
        _questMap.TryGetValue(id, out var quest);
        return quest;
    }

    public bool IsQuestFinished(string id) => _questMap.TryGetValue(id, out var q) && q.state == Enums.QuestState.Finished;

    public void ReportEnemyKilled(EnemyBase enemy)
    {
        if (enemy == null || enemy.Definition == null) return;

        for (int i = 0; i < _activeQuests.Count; i++)
            _activeQuests[i].RouteEnemyKilled(enemy.Definition);
    }

    // Returns true if any active quest's current step requires killing this enemy type
    // and that step has not yet been completed. Used by EnemyQuestMarker.
    public bool IsEnemyDefinitionTargeted(EnemyDefinition definition)
    {
        for (int i = 0; i < _activeQuests.Count; i++)
        {
            var step = _activeQuests[i].GetCurrentStepSO();
            if (step is not KillEnemyStepSO killStep || killStep.IsComplete) continue;
            if (killStep.targetEnemy == null || killStep.targetEnemy == definition) return true;
        }

        return false;
    }

    // Called by VisitZoneTrigger when the player enters a zone collider.
    public void ReportZoneVisited(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;

        for (int i = 0; i < _activeQuests.Count; i++)
            _activeQuests[i].RouteZoneVisited(zoneId);
    }

    // Called by the PlayerInventory.OnItemAdded subscription.
    public void ReportItemCollected(ItemData item, int quantity)
    {
        if (item == null) return;

        for (int i = 0; i < _activeQuests.Count; i++)
            _activeQuests[i].RouteItemCollected(item, quantity);
    }

    // Called by any interactable when the player interacts.
    public void ReportInteraction(string interactionId)
    {
        if (string.IsNullOrEmpty(interactionId)) return;

        _reportedInteractions.Add(interactionId);

        for (int i = 0; i < _activeQuests.Count; i++)
            _activeQuests[i].RouteInteraction(interactionId);
    }

    public bool HasInteractionBeenReported(string id) => _reportedInteractions.Contains(id);

    // Called by PlayerInventory.OnAuraDustChanged.
    public void ReportAuraDustChanged(int newAmount)
    {
        for (int i = 0; i < _activeQuests.Count; i++)
            _activeQuests[i].RouteAuraDustChanged(newAmount);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        data.quests.entries.Clear();

        foreach (var quest in _questMap.Values)
        {
            try
            {
                data.quests.entries.Add(new QuestSaveEntry
                {
                    id = quest.info.id,
                    json = JsonUtility.ToJson(quest.GetQuestData())
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] CaptureToSave failed for '{quest.info.id}': {e}");
            }
        }

        data.quests.reportedInteractions.Clear();
        data.quests.reportedInteractions.AddRange(_reportedInteractions);
    }

    public void ApplyFromSave(SaveData data)
    {
        // Determine safe zone from the scene that just finished loading.
        bool loadingVillage = SceneManager.GetActiveScene().name == SceneNames.Base;
        IsInSafeZone = loadingVillage;

        if (loadingVillage)
        {
            // Returned to Village without a confirmed extraction (player died) →
            // discard pending advances so the quest stays InProgress next run.
            if (!_extractionConfirmed)
                _pendingAdvances.Clear();

            _extractionConfirmed = false;
        }
        else
        {
            // Entering a world scene — no carryover pending from a prior run.
            _pendingAdvances.Clear();
        }

        _reportedInteractions.Clear();
        if (data.quests.reportedInteractions != null)
            _reportedInteractions.UnionWith(data.quests.reportedInteractions);

        // Rebuild the map from scratch so that quests not present in the save
        // (e.g. after WipeSave / new game) start from their default state
        // instead of keeping stale InProgress state from the previous session.
        BuildQuestMap();

        _activeQuests.Clear();

        foreach (var entry in data.quests.entries)
        {
            if (!_questMap.TryGetValue(entry.id, out _)) continue;

            try
            {
                var questData = JsonUtility.FromJson<QuestData>(entry.json);
                _questMap[entry.id] = new Quest(
                    _questMap[entry.id].info,
                    questData.state,
                    questData.questStepIndex,
                    questData.questStepStates);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestService] ApplyFromSave failed for '{entry.id}': {e}");
            }
        }

        foreach (var quest in _questMap.Values)
        {
            if (quest.state == Enums.QuestState.InProgress)
                ActivateQuest(quest);

            OnQuestStateChanged?.Invoke(quest);
        }

        RefreshAllRequirements();

        // Re-seed all active step counters from live inventory after IsSaveApplied,
        // both in Village and in world scenes. PlayerInventory registers after
        // QuestService so CountInInventory() returns 0 during OnActivate —
        // the deferred check here is the authoritative re-seed point.
        if (_activeQuests.Count > 0)
            CheckStepCompletionsAfterSaveAsync().Forget();
    }

    #endregion

    #region Private

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsubscribeFromSceneEvents();
        WaitAndSubscribeSceneReportersAsync().Forget();
    }

    private async UniTaskVoid WaitAndSubscribeSceneReportersAsync()
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
        _subscribedInventory.OnAuraDustChanged += HandleAuraDustChanged;

        // ApplyFromSave fires OnAuraDustChanged on the new inventory instance before this
        // subscription is set up, so that event is always missed. Seed the dust counter
        // immediately so quest steps reflect the permanent _auroraDust without waiting for
        // the next pickup.
        if (_subscribedInventory.AuraDust > 0)
            HandleAuraDustChanged();

        try
        {
            await UniTask.NextFrame(destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (GameServices.TryGet<Gameplay.Enemies.EnemyManager>(out var em))
        {
            _subscribedEnemyManager = em;
            _subscribedEnemyManager.OnEnemyDied += ReportEnemyKilled;
        }
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (_subscribedEnemyManager != null)
        {
            _subscribedEnemyManager.OnEnemyDied -= ReportEnemyKilled;
            _subscribedEnemyManager = null;
        }

        if (_subscribedInventory != null)
        {
            _subscribedInventory.OnItemAdded -= HandleItemAdded;
            _subscribedInventory.OnAuraDustChanged -= HandleAuraDustChanged;
            _subscribedInventory = null;
        }
    }

    // Waits until all ISaveable.ApplyFromSave calls have completed (IsSaveApplied)
    // so that PlayerInventory.ItemStacks and AuraDust are populated before
    // CountInInventory() runs. IsSaveApplied logically implies PlayerInventory is
    // ready, but a defensive second wait handles any edge-case timing difference.
    private async UniTaskVoid CheckStepCompletionsAfterSaveAsync()
    {
        if (GameServices.TryGet<SaveService>(out var save))
        {
            try
            {
                await UniTask.WaitUntil(() => save.IsSaveApplied, cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        // Defensive: IsSaveApplied implies PlayerInventory.ApplyFromSave has run,
        // but guard against edge cases where Instance is still null (e.g. late Awake
        // ordering). CollectItemStepSO.CheckCompletionOnActivate early-outs on null
        // inventory, so this prevents it returning before AuraDust is loaded.
        try
        {
            await UniTask.WaitUntil(() => PlayerInventory.Instance != null, cancellationToken: destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var snapshot = _activeQuests.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
            snapshot[i].CheckCurrentStepCompletion();
    }

    private void HandleItemAdded(ItemStack stack)
    {
        if (stack?.data == null) return;
        ReportItemCollected(stack.data, stack.quantity);
    }

    private void HandleAuraDustChanged()
    {
        int amount = PlayerInventory.Instance != null ? PlayerInventory.Instance.AuraDust : 0;
        ReportAuraDustChanged(amount);

        if (!IsInSafeZone) return;

        foreach (var quest in _questMap.Values)
        {
            if (quest.state != Enums.QuestState.CanFinish) continue;
            if (HasRequiredItemsForFinish(quest)) continue;
            RevertQuestToInProgress(quest);
        }
    }

    private void RevertQuestToInProgress(Quest quest)
    {
        var inv   = PlayerInventory.Instance;
        var steps = quest.info.questSteps;

        for (int i = 0; i < steps.Count; i++)
        {
            ItemData item    = null;
            int      required = 0;

            if (steps[i] is CollectItemStepSO collect)
            {
                item     = collect.targetItem;
                required = collect.requiredQuantity;
            }
            else if (steps[i] is DeliverItemStepSO deliver && deliver.consumeOnDelivery)
            {
                item     = deliver.requiredItem;
                required = deliver.requiredQuantity;
            }

            if (item == null) continue;
            if (HasEnoughInInventory(inv, item, required)) continue;

            quest.RewindToStep(i);

            if (!_activeQuests.Contains(quest))
                _activeQuests.Add(quest);

            quest.ActivateCurrentStep();
            SetState(quest, Enums.QuestState.InProgress);

            if (IsInSafeZone)
                quest.CheckCurrentStepCompletion();

            return;
        }
    }

    private void ActivateQuest(Quest quest)
    {
        if (!_activeQuests.Contains(quest))
            _activeQuests.Add(quest);

        quest.ActivateCurrentStep();
    }

    private void SetState(Quest quest, Enums.QuestState newState)
    {
        Debug.Log($"[Quest] '{quest.info.id}' state: {quest.state} → {newState}.");
        quest.state = newState;
        OnQuestStateChanged?.Invoke(quest);
    }

    private bool TryGetQuest(string id, out Quest quest)
    {
        if (_questMap.TryGetValue(id, out quest)) return true;
        Debug.LogError($"[QuestService] Quest '{id}' not found.");
        return false;
    }

    // Replaces the per-frame Update() poll. Only checks when a quest reaches
    // Finished, which is the only event that can unlock new prereq chains.
    private void HandleQuestStateChangedForRequirements(Quest changedQuest)
    {
        if (changedQuest.state == Enums.QuestState.Finished)
            RefreshAllRequirements();
    }

    private void RefreshAllRequirements()
    {
        foreach (var quest in _questMap.Values)
        {
            if (quest.state != Enums.QuestState.RequirementNotMet) continue;
            if (CheckRequirements(quest))
                SetState(quest, Enums.QuestState.CanStart);
        }
    }

    private bool CheckRequirements(Quest quest)
    {
        var prereqs = quest.info.questPreequisits;
        for (int i = 0; i < prereqs.Length; i++)
        {
            if (prereqs[i] == null)
            {
                Debug.LogWarning($"[QuestService] Quest '{quest.info.id}' has a null entry in questPreequisits at index {i}. Remove the empty slot in the Inspector.");
                continue;
            }

            if (!IsQuestFinished(prereqs[i].id)) return false;
        }

        return true;
    }

    private void BuildQuestMap()
    {
        var allQuests = Resources.LoadAll<QuestInfoSO>("Quests");
        _questMap.Clear();

        foreach (var info in allQuests)
        {
            if (_questMap.ContainsKey(info.id))
            {
                Debug.LogWarning($"[QuestService] Duplicate quest id '{info.id}'. Skipping.");
                continue;
            }

            _questMap.Add(info.id, new Quest(info));
        }
    }

    public UniTask GrantRewardAsync(QuestReward reward)
    {
        return GrantRewardInternalAsync(reward);
    }

    private async UniTaskVoid GrantRewardsAsync(Quest quest)
    {
        var reward = quest.info.reward;
        if (reward == null) return;

        await GrantRewardInternalAsync(reward);
    }

    private async UniTask GrantRewardInternalAsync(QuestReward reward)
    {
        if (reward == null) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[QuestService] PlayerInventory null — cannot grant rewards.");
            return;
        }

        await UniTask.WaitUntil(() => inventory.InventoryInit);

        var overflow = new List<ItemStack>();
        var rewardEntries = new List<RewardEntry>();

        if (reward.items != null)
        {
            foreach (var itemReward in reward.items)
            {
                if (itemReward.item == null) continue;
                var stack = new ItemStack { data = itemReward.item, quantity = itemReward.quantity };
                var failed = await TryPlaceStackAsync(stack, inventory);
                if (failed != null) overflow.Add(failed);

                rewardEntries.Add(new RewardEntry
                {
                    type = RewardType.Item,
                    icon = itemReward.item.icon,
                    label = itemReward.item.localizedDisplayName.GetLocalizedString(),
                    quantity = itemReward.quantity,
                    isRandom = false,
                    rarity = stack.data.itemRarity
                });
            }
        }

        if (reward.randomItems != null)
        {
            foreach (var randomReward in reward.randomItems)
            {
                if (randomReward.pool == null || randomReward.pool.Length == 0) continue;

                var picked = randomReward.pool[UnityEngine.Random.Range(0, randomReward.pool.Length)];
                if (picked == null) continue;

                var stack = new ItemStack { data = picked, quantity = randomReward.quantity };

                if (randomReward.overrideRarity && picked is EquipableItemData equipable)
                {
                    stack.isInstanced = true;
                    stack.runtimeInstanceId = Guid.NewGuid().ToString();
                    stack.rolledRarity = randomReward.forcedRarity;
                    stack.rolledModifiers = RollModifiers(equipable, randomReward.forcedRarity);
                }

                var failed = await TryPlaceStackAsync(stack, inventory);
                if (failed != null) overflow.Add(failed);

                rewardEntries.Add(new RewardEntry
                {
                    type = RewardType.Item,
                    icon = picked.icon,
                    label = picked.localizedDisplayName.GetLocalizedString(),
                    quantity = randomReward.quantity,
                    isRandom = true,
                    rarity = randomReward.overrideRarity
                            ? randomReward.forcedRarity
                            : picked.itemRarity
                });
            }
        }

        if (overflow.Count > 0)
            DropStacksToWorld(overflow);

        if (reward.auroraDust > 0)
        {
            PlayerInventory.Instance.AddAuraDust(reward.auroraDust);
            rewardEntries.Add(new RewardEntry
            {
                type = RewardType.AuroraDust,
                quantity = reward.auroraDust,
                    isRandom = false,
            });
        }

        if (rewardEntries.Count > 0)
            OnRewardsGranted?.Invoke(rewardEntries);
    }

    // Returns null if placed successfully, or the stack itself if the inventory was full.
    private async UniTask<ItemStack> TryPlaceStackAsync(ItemStack stack, PlayerInventory inventory)
    {
        var visual = new ItemVisual(stack, inventory);
        stack.RootVisual = visual;
        inventory.GetInventoryGrid().Add(visual);

        bool placed = await inventory.AutoPlaceItem(visual);
        if (placed)
        {
            inventory.AddStack(stack);
            visual.OriginContainer = inventory;
            visual.style.visibility = Visibility.Visible;
            return null;
        }

        inventory.GetInventoryGrid().Remove(visual);
        visual.CleanupRotatedAssets();
        return stack;
    }

    private static List<StatModifier> RollModifiers(EquipableItemData equipable, Enums.ItemRarity rarity)
    {
        var result = new List<StatModifier>();
        if (equipable.modifiers == null) return result;

        var (min, max) = RarityUtility.GetMultiplierRange(rarity);
        foreach (var mod in equipable.modifiers)
        {
            if (mod == null) continue;
            var rolled = mod.Clone();
            rolled.value = mod.value * UnityEngine.Random.Range(min, max);
            result.Add(rolled);
        }

        return result;
    }

    private void DropStacksToWorld(List<ItemStack> stacks)
    {
        var player = PlayerInventory.Instance?.player;
        if (player == null) return;

        Vector3 pos = player.transform.position + player.transform.forward;
        if (GameServices.TryGet<LootManager>(out var lm))
            lm.SpawnLootFromStacks(stacks, pos);
    }

    #endregion
}