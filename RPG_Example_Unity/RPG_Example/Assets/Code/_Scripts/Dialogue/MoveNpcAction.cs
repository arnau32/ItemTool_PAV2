using UnityEngine;

[CreateAssetMenu(fileName = "MoveNpcAction", menuName = "Dialogue/Actions/Move NPC")]
public class MoveNpcAction : DialogueAction
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
            Debug.LogWarning($"[MoveNpcAction] No NpcMover with id '{_npcMoverId}' found in scene.");
            return;
        }

        mover.TeleportTo(_targetPosition, _targetYRotation);
    }

    #endregion
}
