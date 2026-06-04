using UnityEngine;

[CreateAssetMenu(fileName = "LevelUpNpcAction", menuName = "Dialogue/Actions/Level Up NPC")]
public class LevelUpNpcAction : DialogueAction
{
    [SerializeField] private HealerNpcSO _npcData;

    public override void Execute()
    {
        if (_npcData == null)
        {
            Debug.LogWarning("[LevelUpNpcAction] _npcData not assigned.");
            return;
        }

        if (!GameServices.TryGet<NpcLevelService>(out var svc))
        {
            Debug.LogWarning("[LevelUpNpcAction] NpcLevelService not found.");
            return;
        }

        svc.LevelUp(_npcData.npcId, _npcData.maxLevel);
    }
}
