using UnityEngine;

// Playable action triggered when the player interacts with a Storage object.
// Storage injects itself via PrepareForStorage() before PlayerInteractable calls StartAction().
// OnCompleted opens the loot UI — Storage never references the player directly.
[CreateAssetMenu(fileName = "ChestOpenAction", menuName = "Player/Playable Actions/Chest Open")]
public class ChestOpenPlayableAction : PlayerPlayableAction
{
    [System.NonSerialized] public Storage PendingStorage;

    public void PrepareForStorage(Storage storage)
    {
        PendingStorage = storage;
    }

    public override void OnCompleted(PlayerContext context)
    {
        if (PendingStorage == null)
        {
            Debug.LogWarning("[ChestOpenPlayableAction] OnCompleted called but PendingStorage is null.");
            return;
        }

        PendingStorage.OnInteract();
        PendingStorage = null;
    }

    public override void OnInterrupted(PlayerContext context)
    {
        PendingStorage = null;
    }
}