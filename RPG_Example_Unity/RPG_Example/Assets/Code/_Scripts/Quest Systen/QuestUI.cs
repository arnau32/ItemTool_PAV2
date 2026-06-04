using TMPro;
using UnityEngine;

public class QuestUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _questText;
    
    public TextMeshProUGUI GetQuestText=> _questText;
}
