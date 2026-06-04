using UnityEngine;

// Playable action triggered when the player uses a consumable from a quick-slot.
// Stores the ItemStack reference at start time so OnCompleted can apply its effect.
// The stack was already decremented in PlayerConsumableController before starting the action;
// this object only triggers the buffered Use() call that was deferred.

[CreateAssetMenu(fileName = "ConsumeAction", menuName = "Player/Playable Actions/Consume")]
public class ConsumePlayableAction : PlayerPlayableAction
{
    [System.NonSerialized] public ItemStack PendingStack;
    [System.NonSerialized] public GameObject OwnerGameObject;

    public override void OnCompleted(PlayerContext context)
    {
        if (PendingStack == null)
        {
            Debug.LogWarning("[ConsumePlayableAction] OnCompleted called but PendingStack is null.");
            return;
        }

        // ItemStack.Use applies buffs (heals, stat mods, etc.) defined in ConsumableItemData.
        PendingStack.Use(OwnerGameObject);

        if (PendingStack.IsEmpty)
        {
            var slot = ContainerRegistry.GetConsumableSlotForStack(PendingStack);
            slot?.RemoveItemStack(PendingStack);
        }
        else
        {
            PendingStack.RootVisual?.UpdateCountLabel();
        }

        ConsumableItemSlotManager.Instance.RefreshAllSlots();

        PendingStack      = null;
        OwnerGameObject   = null;
    }

    public override void OnInterrupted(PlayerContext context)
    {
        // Item is not consumed — clear pending refs without applying any effect.
        // The stack quantity was NOT decremented yet (consume happens only on completion).
        PendingStack    = null;
        OwnerGameObject = null;
    }
}