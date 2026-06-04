using UnityEngine;

public class TutorialConsumableHintTrigger : MonoBehaviour
{
    [SerializeField] private TutorialConsumableHint _hint;

    #region Unity Callbacks

    private void Start()
    {
        if (GameServices.TryGet<SaveService>(out var save) && save.CurrentSave.meta.tutorialConsumableHintShown)
        {
            enabled = false;
            return;
        }

        TabViewManager.Instance.OnTabClose += OnTabClosed;
    }

    private void OnDestroy()
    {
        if (TabViewManager.Instance != null)
            TabViewManager.Instance.OnTabClose -= OnTabClosed;
    }

    #endregion

    #region Helpers

    private void OnTabClosed()
    {
        if (!HasConsumableInInventory()) return;

        TabViewManager.Instance.OnTabClose -= OnTabClosed;

        if (GameServices.TryGet<SaveService>(out var save))
        {
            save.CurrentSave.meta.tutorialConsumableHintShown = true;
            save.SaveImmediate();
        }

        _hint.enabled = true;
        enabled = false;
    }

    private static bool HasConsumableInInventory()
    {
        var stacks = PlayerInventory.Instance.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data != null && stacks[i].data.itemType == Enums.ItemType.Consumable)
                return true;
        }

        // Hold-loot places consumables directly into D-pad slots, bypassing ItemStacks.
        for (int i = 0; i < 4; i++)
        {
            var slot = ContainerRegistry.GetConsumableSlot(i);
            if (slot?.currentStack?.data != null &&
                slot.currentStack.data.itemType == Enums.ItemType.Consumable)
                return true;
        }

        return false;
    }

    #endregion
}
