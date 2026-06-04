using UnityEngine;

// Handles consumable item usage from dpad input.
// Animation is now handled by PlayerPlayableController — this controller only
// validates the slot and injects the pending stack into the ConsumePlayableAction.
public class PlayerConsumableController : MonoBehaviour
{
    [SerializeField] private ConsumePlayableAction _consumeAction;

    private PlayerContext _context;

    #region Public API

    public void Initialize(PlayerContext context)
    {
        _context = context;
        _context.Inputs.OnUseConsumable += HandleUseConsumable;
    }

    #endregion

    #region Unity Callbacks

    private void OnDisable()
    {
        if (_context?.Inputs == null) return;

        _context.Inputs.OnUseConsumable -= HandleUseConsumable;
    }

    #endregion

    #region Helpers

    private void HandleUseConsumable(Vector2 dpadInput)
    {
        if (_context.PlayableController.IsInAction) return;

        int slotNumber = DpadToSlot(dpadInput);
        if (slotNumber < 0) return;

        var slot = ContainerRegistry.GetConsumableSlot(slotNumber);
        if (slot == null || slot.currentStack == null) return;

        if (_consumeAction == null)
        {
            Debug.LogWarning("[PlayerConsumableController] ConsumePlayableAction not assigned.", this);
            return;
        }

        _consumeAction.PendingStack = slot.currentStack;
        _consumeAction.OwnerGameObject = _context.Owner;

        _context.PlayableController.StartAction(_consumeAction);
    }

    /// up=0 / left=1 / right=2 / down=3
    private static int DpadToSlot(Vector2 input)
    {
        if (input == Vector2.up) return 0;
        if (input == Vector2.left) return 1;
        if (input == Vector2.right) return 2;
        if (input == Vector2.down) return 3;
        return -1;
    }

    #endregion
}