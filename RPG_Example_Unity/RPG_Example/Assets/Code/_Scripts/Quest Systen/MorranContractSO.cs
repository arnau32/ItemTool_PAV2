using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "MorranContract", menuName = "Quests/Morran Contract")]
public class MorranContractSO : ScriptableObject
{
    [field: SerializeField] public string contractId { get; private set; }

    [System.Serializable]
    public struct RequiredItem
    {
        public ItemData item;
        [Min(1)] public int quantity;
    }

    [Header("Display")]
    public LocalizedString contractName;
    public LocalizedString contractDescription;

    [Header("Requirements")]
    public RequiredItem[] requiredItems;

    [Header("Reward")]
    public QuestReward reward;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(contractId)) return;
        var so = new UnityEditor.SerializedObject(this);
        so.FindProperty("<contractId>k__BackingField").stringValue = System.Guid.NewGuid().ToString();
        so.ApplyModifiedPropertiesWithoutUndo();
    }
#endif
}
