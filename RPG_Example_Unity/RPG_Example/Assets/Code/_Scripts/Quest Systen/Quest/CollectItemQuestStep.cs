using UnityEngine;

/// <summary>
/// Quest step: collect N items of a specific type.
/// Progress is shown as "objectiveText (current/required)".
///
/// Counter is seeded from the existing inventory on activation so that items
/// already saved in Village (from prior runs) count immediately.
/// During a world run, the counter accumulates but Complete() only registers
/// a pending advance — QuestService confirms it on successful extraction.
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Collect Item")]
public class CollectItemStepSO : QuestStepSO
{
    [Header("Objective")]
    public ItemData targetItem;
    [Min(1)] public int requiredQuantity = 1;

    [System.NonSerialized] private int _collected;

    protected override void OnActivate(string savedState)
    {
        _collected = 0;

        if (!string.IsNullOrEmpty(savedState) && int.TryParse(savedState, out int saved))
            _collected = saved;

        // CountInInventory() is intentionally NOT called here:
        // QuestService registers before PlayerInventory, so ApplyFromSave
        // for this step runs before inventory data is restored.
        // The re-seed from live inventory happens in CheckCompletionOnActivate,
        // which is called by CheckStepCompletionsAfterSaveAsync after IsSaveApplied.
    }

    protected override void PushInitialStatus() => PushProgress(_collected, requiredQuantity);

    public override string GetSaveState() => _collected.ToString();

    // Called by CheckStepCompletionsAfterSaveAsync after IsSaveApplied and
    // PlayerInventory.Instance is confirmed non-null.
    //
    // Aurora Dust: syncs _collected in both directions (up AND down).
    //   Permanent dust (_auroraDust) survives death, but collectables in
    //   inventory don't. A saved _collected from a discarded pending advance
    //   (e.g. 50) must clamp to the dust actually held (e.g. 25 perm = 25/50).
    //
    // Regular items: only bumps up. Items legitimately stored in village chests
    //   are not in the player's backpack but should still count as "collected".
    //   If items were lost on death (KeepOnEnter restores snapshot), CountInInventory
    //   returns 0 and _collected from the save remains, but Complete() is blocked
    //   by the inInventory >= requiredQuantity guard — preventing phantom completion.
    public override void CheckCompletionOnActivate()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        int inInventory = CountInInventory();
        bool isDust = targetItem is CollectableItemData d && d.isAuroraDust;

        if (isDust)
        {
            // Aurora Dust: always sync to live inventory (clamp both up and down).
            int synced = Mathf.Min(inInventory, requiredQuantity);
            if (synced != _collected)
            {
                _collected = synced;
                PushProgress(_collected, requiredQuantity);
            }
        }
        else if (inInventory > _collected)
        {
            // Regular items: only advance the peak, never regress it.
            _collected = Mathf.Min(inInventory, requiredQuantity);
            PushProgress(_collected, requiredQuantity);
        }

        // For both types: only complete when current inventory actually meets
        // the requirement — prevents phantom completions after a discarded
        // pending advance leaves _collected = requiredQuantity in the save.
        if (_collected >= requiredQuantity && inInventory >= requiredQuantity)
            Complete();
    }

    public override void OnItemCollected(ItemData item, int quantity)
    {
        if (_isComplete) return;
        if (item == null || targetItem == null) return;
        if (item.uniqueID != targetItem.uniqueID) return;

        // Aurora Dust: use inventory snapshot so moving items in/out of chests
        // doesn't double-count. Only advance when the snapshot exceeds current peak.
        if (targetItem is CollectableItemData dustData && dustData.isAuroraDust)
        {
            TryAdvanceDustProgress();
            return;
        }

        _collected = Mathf.Min(_collected + quantity, requiredQuantity);
        PushProgress(_collected, requiredQuantity);

        if (_collected >= requiredQuantity)
            Complete();
    }

    public override void OnAuraDustChanged(int newAmount)
    {
        if (_isComplete) return;
        if (!(targetItem is CollectableItemData d) || !d.isAuroraDust) return;

        // Currency changed (pickup, extraction, or spend). Only advance — never
        // decrease _collected when the player spends Aurora Dust on upgrades.
        TryAdvanceDustProgress();
    }

    // Advances _collected only when the current inventory snapshot exceeds the
    // stored peak. Prevents both chest double-counting and upgrade-spend regression.
    private void TryAdvanceDustProgress()
    {
        int current = Mathf.Min(CountInInventory(), requiredQuantity);
        if (current <= _collected) return;

        _collected = current;
        PushProgress(_collected, requiredQuantity);

        if (_collected >= requiredQuantity)
            Complete();
    }

    private int CountInInventory()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || targetItem == null) return 0;

        int total = 0;
        var stacks = inv.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            var s = stacks[i];
            if (s.data != null && s.data.uniqueID == targetItem.uniqueID)
                total += s.quantity;
        }

        if (targetItem is CollectableItemData dustData && dustData.isAuroraDust)
            total += inv.AuraDust;

        return total;
    }
}
