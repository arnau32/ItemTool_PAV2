using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "QuestData", menuName = "Quests/QuestInfo", order = 0)]
public class QuestInfoSO : ScriptableObject
{
    [field: SerializeField] public string id { get; private set; }

    [Header("General info: ")]
    public LocalizedString qName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Requirements :")]
    public QuestInfoSO[] questPreequisits;

    [Header("Steps")]
    [Tooltip("Ordered list of SO steps. Each step activates when the previous one completes.")]
    public List<QuestStepSO> questSteps = new List<QuestStepSO>();

    [Tooltip("Shown in the HUD once all steps are done and the quest is waiting to be turned in.")]
    public LocalizedString canFinishText;

    [Header("Map POIs")]
    [Tooltip("POIs shown on the map while this quest is InProgress or CanFinish. Removed when the quest is Finished.")]
    public MapPOIData[] mapPOIs;

    [Header("Rewards")]
    public QuestReward reward;

    private void OnValidate()
    {
        #if UNITY_EDITOR
        id = this.name;
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
}
