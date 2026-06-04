using UnityEngine;

/// <summary>
/// Quest step: deliver N items total to a zone, across one or more visits.
///
/// Each visit consumes as many items as the player currently carries (up to
/// the remaining quota), adds them to a persistent delivered counter, and
/// updates the HUD. The step completes when the counter reaches requiredQuantity.
///
/// Example — deliver 10 herbs:
///   Visit 1: player has 3 → delivers 3, counter = 3/10
///   Visit 2: player has 5 → delivers 5, counter = 8/10
///   Visit 3: player has 4 → delivers 2 (only 2 remain), counter = 10/10 → complete
///
/// Save state: the delivered counter is serialized so progress survives scene reloads.
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Deliver Item")]
public class DeliverItemStepSO : QuestStepSO
{
    [Header("Objective")]
    public ItemData requiredItem;
    [Min(1)] public int requiredQuantity = 1;
    public bool consumeOnDelivery = true;

    [Header("Zone")]
    [Tooltip("Must match the zoneId on the VisitZoneTrigger in the Level scene.")]
    public string deliveryZoneId;

    // Accumulated delivered count — persisted across scene reloads via GetSaveState().
    private int _totalDelivered;

    protected override void OnActivate(string savedState)
    {
        // Restore accumulated progress from save if available.
        _totalDelivered = int.TryParse(savedState, out int saved) ? saved : 0;
    }

    protected override void PushInitialStatus()
    {
        int display = Mathf.Max(_totalDelivered, Mathf.Min(CountInInventory(), requiredQuantity));
        PushProgress(display, requiredQuantity);
    }

    // Serialize delivered count so partial progress survives scene reloads.
    public override string GetSaveState() => _totalDelivered.ToString();

    public override void CheckCompletionOnActivate()
    {
        int inInventory = CountInInventory();
        Debug.Log($"[Quest] '{_questId}' step activated — {inInventory}/{requiredQuantity} '{requiredItem?.name}' in inventory.");

        if (inInventory >= requiredQuantity)
            Complete();
    }

    public override void OnItemCollected(ItemData item, int quantity)
    {
        if (_isComplete) return;
        if (item == null || requiredItem == null) return;
        if (item.uniqueID != requiredItem.uniqueID) return;

        int inInventory = Mathf.Min(CountInInventory(), requiredQuantity);
        Debug.Log($"[Quest] '{_questId}' item collected — {inInventory}/{requiredQuantity} '{item.name}'.");
        PushProgress(inInventory, requiredQuantity);

        if (inInventory >= requiredQuantity)
            Complete();
    }

    public override void OnAuraDustChanged(int newAmount)
    {
        if (_isComplete) return;
        if (!(requiredItem is CollectableItemData d) || !d.isAuroraDust) return;

        int inInventory = Mathf.Min(CountInInventory(), requiredQuantity);
        PushProgress(inInventory, requiredQuantity);

        if (inInventory >= requiredQuantity)
            Complete();
    }

    public override void OnZoneVisited(string id)
    {
        if (_isComplete) return;
        if (id != deliveryZoneId) return;
        if (requiredItem == null) return;

        int inInventory = CountInInventory();
        if (inInventory <= 0)
        {
            // Player arrived with none — just refresh HUD to show current state.
            PushProgress(_totalDelivered, requiredQuantity);
            return;
        }

        // Deliver as many as possible without exceeding the remaining quota.
        int remaining = requiredQuantity - _totalDelivered;
        int toDeliver = Mathf.Min(inInventory, remaining);

        if (consumeOnDelivery)
            ConsumeItems(toDeliver);

        _totalDelivered += toDeliver;
        PushProgress(_totalDelivered, requiredQuantity);

        if (_totalDelivered >= requiredQuantity)
            Complete();
    }

    /// <summary>
    /// Forces this step to completion from a DialogueAction, bypassing the zone trigger.
    /// Consumes the outstanding items if consumeItems is true.
    /// Safe to call when the step is already complete (no-op).
    /// </summary>
    public void ForceComplete(bool consumeItems)
    {
        if (_isComplete) return;

        int remaining = requiredQuantity - _totalDelivered;
        if (consumeItems && remaining > 0)
            ConsumeItems(remaining);

        _totalDelivered = requiredQuantity;
        PushProgress(_totalDelivered, requiredQuantity);
        Complete();
    }

    private int CountInInventory()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return 0;

        int total = 0;
        var stacks = inv.ItemStacks;

        for (int i = 0; i < stacks.Count; i++)
        {
            var s = stacks[i];
            if (s.data != null && s.data.uniqueID == requiredItem.uniqueID)
                total += s.quantity;
        }

        if (requiredItem is CollectableItemData dustData && dustData.isAuroraDust)
            total += inv.AuraDust;

        return total;
    }

    private void ConsumeItems(int amount)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        var stacks    = inv.ItemStacks;
        int remaining = amount;

        for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var s = stacks[i];
            if (s.data == null || s.data.uniqueID != requiredItem.uniqueID) continue;

            int toRemove = Mathf.Min(remaining, s.quantity);
            s.quantity  -= toRemove;
            remaining   -= toRemove;

            if (s.quantity <= 0)
                inv.RemoveItemStack(s);
            else
                s.RootVisual?.UpdateCountLabel();
        }

        // Spend any outstanding amount from extracted AuroraDust currency.
        if (remaining > 0 && requiredItem is CollectableItemData dustData && dustData.isAuroraDust)
            inv.TrySpend(remaining);
    }
}