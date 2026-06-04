using UnityEngine;
using UnityEngine.UIElements;

public class KeyCountUI : MonoBehaviour
{
    private const string QUEST_ID = "Open Door";

    private Label        _keyLabel;
    private QuestService _questService;

    private void Start()
    {
        var panel = UIManager.Instance.tabViewPanel;
        _keyLabel = panel.Q<Label>("Key_Label");

        if (!GameServices.TryGet<QuestService>(out _questService))
        {
            SetCount(0);
            return;
        }

        _questService.OnQuestStateChanged += OnQuestStateChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_questService != null)
            _questService.OnQuestStateChanged -= OnQuestStateChanged;
    }

    private void OnQuestStateChanged(Quest quest)
    {
        if (quest.info.id == QUEST_ID)
            Refresh();
    }

    private void Refresh()
    {
        var quest = _questService.GetQuestById(QUEST_ID);
        bool active = quest != null && quest.state is
            Enums.QuestState.InProgress or
            Enums.QuestState.CanFinish;

        SetCount(active ? 1 : 0);
    }

    private void SetCount(int count)
    {
        if (_keyLabel != null)
            _keyLabel.text = count.ToString();
    }
}
