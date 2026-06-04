using UnityEngine;

/// Like MoveNpcAction but wraps the teleport inside a FadeTransitionSequencer.
/// The NPC is moved at full opacity (midpoint), hidden from the player.
/// Falls back to an immediate teleport if no sequencer is present in the scene.
[CreateAssetMenu(fileName = "FadedMoveNpcAction", menuName = "Dialogue/Actions/Faded Move NPC")]
public class FadedMoveNpcAction : DialogueAction
{
    #region Fields

    [Tooltip("Must match the _npcMoverId set on the NpcMover component in the scene.")]
    [SerializeField] private string _npcMoverId;

    [SerializeField] private Vector3 _targetPosition;
    [SerializeField] private float _targetYRotation;

    #endregion

    #region DialogueAction

    public override void Execute()
    {
        if (!NpcMover.TryGet(_npcMoverId, out var mover))
        {
            Debug.LogWarning($"[FadedMoveNpcAction] No NpcMover with id '{_npcMoverId}' found in scene.");
            return;
        }

        if (!FadeTransitionSequencer.TryGetInstance(out var sequencer))
        {
            Debug.LogWarning("[FadedMoveNpcAction] No FadeTransitionSequencer found in scene — teleporting without fade.");
            mover.TeleportTo(_targetPosition, _targetYRotation);
            return;
        }

        sequencer.PlayTransitionWithAction(() => mover.TeleportTo(_targetPosition, _targetYRotation));
    }

    #endregion
}
