using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class QuestTabManager : MonoBehaviour
{
    private const string MORRAN_CONTRACT_KEY = "morran_contract";

    private UIDocument doc;

    private VisualElement QuestTabPanel;
    private ScrollView container;

    private QuestService          questService;
    private MorranContractService _morranService;

    private Dictionary<string, Label> activeLabels    = new();
    private Dictionary<string, Label> completedLabels = new();

    void Start()
    {
        doc = GetComponent<UIDocument>();
        QuestTabPanel = doc.rootVisualElement.Q<VisualElement>("QuestTabPanel");

        container = QuestTabPanel?.Q<ScrollView>("quest-container");

        if (container == null)
        {
            Debug.LogError("[QuestTabManager] 'quest-container' ScrollView not found in UIDocument.", this);
            return;
        }

        if (GameServices.TryGet(out _morranService))
        {
            _morranService.OnContractChanged += OnContractChanged;
            WaitAndRefreshContractAsync().Forget();
        }
    }

    // ApplyFromSave runs before Start() but IsSaveApplied is set synchronously
    // at the end of ApplyAll(). We wait one frame so all Awake/Start ordering
    // edge cases settle, then read the live service state.
    private async UniTaskVoid WaitAndRefreshContractAsync()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            await UniTask.WaitUntil(() => save.IsSaveApplied);

        if (_morranService != null && _morranService.HasActive)
            ShowOrUpdateContract(_morranService.ActiveContract);
    }

    void OnDestroy()
    {
        if (questService != null)
        {
            questService.OnQuestStateChanged -= OnQuestChanged;
            questService.OnStepCompleted     -= OnStepCompleted;
        }

        if (_morranService != null)
            _morranService.OnContractChanged -= OnContractChanged;
    }

    // ������������������������������

    void OnQuestChanged(Quest quest)
    {
        switch (quest.state)
        {
            case Enums.QuestState.InProgress:
            case Enums.QuestState.CanFinish:
                ShowOrUpdate(quest);
                break;

            case Enums.QuestState.Finished:
                Remove(quest.info.id);
                RemoveCompleted(quest.info.id);
                break;
        }
    }

    void OnStepCompleted(Quest quest, QuestStepSO step, bool isLast)
    {
        string id = quest.info.id;

        RemoveCompleted(id);

        var label = CreateLabel($"{step.objectiveText} (Completed)");
        label.AddToClassList("quest-completed");

        container.Add(label);
        completedLabels[id] = label;

        if (!isLast)
        {
            StartCoroutine(RemoveAfter(id, 3f));
        }
    }

    // ������������������������������

    void ShowOrUpdate(Quest quest)
    {
        if (!activeLabels.TryGetValue(quest.info.id, out var label))
        {
            label = CreateLabel("");
            container.Add(label);
            activeLabels[quest.info.id] = label;
        }

        label.text = quest.GetCurrentStepDisplayText();
        label.RemoveFromClassList("quest-completed");
    }

    void Remove(string id)
    {
        if (!activeLabels.TryGetValue(id, out var label)) return;

        container.Remove(label);
        activeLabels.Remove(id);
    }

    void RemoveCompleted(string id)
    {
        if (!completedLabels.TryGetValue(id, out var label)) return;

        container.Remove(label);
        completedLabels.Remove(id);
    }

    IEnumerator RemoveAfter(string id, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        RemoveCompleted(id);
    }

    Label CreateLabel(string text)
    {
        var label = new Label(text);
        label.AddToClassList("quest-item");
        return label;
    }

    void RefreshAll()
    {
        foreach (var quest in questService.GetAllQuests())
        {
            if (quest.state == Enums.QuestState.InProgress ||
                quest.state == Enums.QuestState.CanFinish)
            {
                ShowOrUpdate(quest);
            }
        }
    }

    void OnContractChanged(MorranContractSO contract)
    {
        if (contract == null)
        {
            Remove(MORRAN_CONTRACT_KEY);
            return;
        }

        ShowOrUpdateContract(contract);
    }

    void ShowOrUpdateContract(MorranContractSO contract)
    {
        if (!activeLabels.TryGetValue(MORRAN_CONTRACT_KEY, out var label))
        {
            label = CreateLabel("");
            container.Add(label);
            activeLabels[MORRAN_CONTRACT_KEY] = label;
        }

        label.text = contract.contractName.GetLocalizedString()
                     + "\n"
                     + contract.contractDescription.GetLocalizedString();
        label.RemoveFromClassList("quest-completed");
    }
}
