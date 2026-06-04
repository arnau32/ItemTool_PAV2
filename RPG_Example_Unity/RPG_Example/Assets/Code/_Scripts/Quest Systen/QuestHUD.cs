using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages the in-game quest text display.
/// Observes QuestService.OnQuestStateChanged to update UI.
/// Lives in the scene — NOT a service.
/// </summary>
public class QuestHUD : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _questPanel;

    [Header("Prefab")]
    [SerializeField] private GameObject _questTextPrefab;
    [SerializeField] private RectTransform _questTextContainer;

    [Header("Step Completion Feedback")]
    [Tooltip("Seconds the green 'Completed' line lingers before auto-removing (non-last step only).")]
    [SerializeField] private float _completedLingerDuration = 3f;

    [Tooltip("Color used for the completed-step line.")]
    [SerializeField] private Color _completedColor = new Color(0.3f, 0.95f, 0.4f);

    // ── Internal state ────────────────────────────────────────────────────────

    private const string MORRAN_CONTRACT_KEY = "morran_contract";

    private QuestService _questService;
    private MorranContractService _morranService;
    private CoroutineRunner _coroutineRunner;

    // Normal active quest UI lines. Key: quest id.
    private readonly Dictionary<string, QuestUI> _activeQuestUIs = new(8);

    // Completion feedback UI lines. Key: quest id.
    private readonly Dictionary<string, QuestUI> _completedQuestUIs = new(4);

    // Coroutines owned by CoroutineRunner — must be stopped via _coroutineRunner.StopCoroutine().
    private readonly Dictionary<string, Coroutine> _completedRoutines = new(4);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (!GameServices.TryGet<QuestService>(out _questService))
        {
            Debug.LogError("[QuestHUD] QuestService not found.");
            return;
        }

        if (!GameServices.TryGet<CoroutineRunner>(out _coroutineRunner))
        {
            Debug.LogError("[QuestHUD] CoroutineRunner not found. Linger timers will not work.");
            return;
        }

        _questService.OnQuestStateChanged += HandleQuestStateChanged;
        _questService.OnStepCompleted += HandleStepCompleted;

        if (GameServices.TryGet(out _morranService))
        {
            _morranService.OnContractChanged         += HandleContractChanged;
            _morranService.OnContractProgressChanged += HandleContractChanged;
        }

        // Defer the initial refresh by one frame so QuestService.CheckStepCompletionsAfterSaveAsync
        // can re-seed counters from live inventory (e.g. auroraDust) before we read the display text.
        // Without the delay, steps that restore _collected=0 from save would show "0/50" for one frame
        // even when the player already holds the required amount of permanent currency.
        WaitAndRefreshAsync().Forget();
    }

    private void OnDestroy()
    {
        if (_questService != null)
        {
            _questService.OnQuestStateChanged -= HandleQuestStateChanged;
            _questService.OnStepCompleted -= HandleStepCompleted;
        }

        if (_morranService != null)
        {
            _morranService.OnContractChanged         -= HandleContractChanged;
            _morranService.OnContractProgressChanged -= HandleContractChanged;
        }

        StopAllLingerRoutines();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleQuestStateChanged(Quest quest)
    {
        switch (quest.state)
        {
            case Enums.QuestState.InProgress:
                ShowOrUpdateQuestText(quest);
                break;

            case Enums.QuestState.CanFinish:
                if (_completedQuestUIs.ContainsKey(quest.info.id))
                    RemoveQuestText(quest.info.id); // green line covers it — clear the old white progress line
                else
                    ShowOrUpdateQuestText(quest);
                break;

            case Enums.QuestState.Finished:
                RemoveQuestText(quest.info.id);
                RemoveCompletedLine(quest.info.id);
                break;

            default:
                break;
        }
    }

    private void HandleStepCompleted(Quest quest, QuestStepSO completedStep, bool isLastStep)
    {
        string questId = quest.info.id;

        // Last step: show canFinishText ("Talk to NPC") in green so the player sees
        // exactly what to do next — no separate white active line is added.
        // Intermediate steps: show the just-completed objective text in green.
        string completedLabel;
        if (isLastStep && quest.info.canFinishText != null && !quest.info.canFinishText.IsEmpty)
            completedLabel = $"{quest.info.canFinishText.GetLocalizedString()} (Completed)";
        else if (completedStep != null)
            completedLabel = $"{completedStep.objectiveText.GetLocalizedString()} (Completed)";
        else
            completedLabel = "Step (Completed)";

        RemoveCompletedLine(questId);

        QuestUI questUI = CreateQuestUI();

        TextMeshProUGUI tmp = questUI.GetQuestText;
        tmp.text = completedLabel;
        tmp.color = _completedColor;

        _completedQuestUIs[questId] = questUI;

        if (!isLastStep)
        {
            Coroutine routine = _coroutineRunner.StartCoroutine(
                LingerRoutine(questId, _completedLingerDuration));

            _completedRoutines[questId] = routine;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleContractChanged(MorranContractSO contract)
    {
        if (contract == null)
        {
            RemoveQuestText(MORRAN_CONTRACT_KEY);
            return;
        }

        if (!_activeQuestUIs.TryGetValue(MORRAN_CONTRACT_KEY, out QuestUI questUI))
        {
            questUI = CreateQuestUI();
            _activeQuestUIs[MORRAN_CONTRACT_KEY] = questUI;
        }

        questUI.GetQuestText.color = Color.white;
        questUI.GetQuestText.text  = BuildContractText(contract);

        UpdatePanelVisibility();
    }

    private string BuildContractText(MorranContractSO contract)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(contract.contractName.GetLocalizedString());

        var items = contract.requiredItems;
        var inventory = PlayerInventory.Instance;

        if (items != null && items.Length == 1)
        {
            int current = Mathf.Min(CountInInventory(inventory, items[0].item), items[0].quantity);
            sb.Append('\n');
            sb.Append(contract.contractDescription.GetLocalizedString());
            sb.Append(" (");
            sb.Append(current);
            sb.Append('/');
            sb.Append(items[0].quantity);
            sb.Append(')');
        }
        else
        {
            sb.Append('\n');
            sb.Append(contract.contractDescription.GetLocalizedString());

            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    int current = Mathf.Min(CountInInventory(inventory, items[i].item), items[i].quantity);
                    sb.Append('\n');
                    sb.Append(items[i].item.localizedDisplayName.GetLocalizedString());
                    sb.Append(" (");
                    sb.Append(current);
                    sb.Append('/');
                    sb.Append(items[i].quantity);
                    sb.Append(')');
                }
            }
        }

        return sb.ToString();
    }

    private static int CountInInventory(PlayerInventory inventory, ItemData item)
    {
        if (inventory == null || !inventory.InventoryInit) return 0;
        int total = 0;
        for (int i = 0; i < inventory.ItemStacks.Count; i++)
        {
            if (inventory.ItemStacks[i].data == item)
                total += inventory.ItemStacks[i].quantity;
        }
        return total;
    }

    private async UniTaskVoid WaitAndRefreshAsync()
    {
        try
        {
            await UniTask.WaitUntil(
                () => PlayerInventory.Instance != null && PlayerInventory.Instance.InventoryInit,
                cancellationToken: destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RefreshAllActiveQuests();
    }

    private void RefreshAllActiveQuests()
    {
        foreach (var quest in _questService.GetAllQuests())
        {
            if (quest.state == Enums.QuestState.InProgress ||
                quest.state == Enums.QuestState.CanFinish)
            {
                ShowOrUpdateQuestText(quest);
            }
        }

        if (_morranService != null && _morranService.HasActive)
            HandleContractChanged(_morranService.ActiveContract);

        UpdatePanelVisibility();
    }

    private void ShowOrUpdateQuestText(Quest quest)
    {
        if (!_activeQuestUIs.TryGetValue(quest.info.id, out QuestUI questUI))
        {
            questUI = CreateQuestUI();
            _activeQuestUIs[quest.info.id] = questUI;
        }

        TextMeshProUGUI tmp = questUI.GetQuestText;

        tmp.color = Color.white;
        tmp.text = quest.GetCurrentStepDisplayText();

        UpdatePanelVisibility();
    }

    private void RemoveQuestText(string questId)
    {
        if (!_activeQuestUIs.TryGetValue(questId, out QuestUI questUI)) return;

        if (questUI != null)
            Destroy(questUI.gameObject);

        _activeQuestUIs.Remove(questId);
        UpdatePanelVisibility();
    }

    private void RemoveCompletedLine(string questId)
    {
        if (_completedRoutines.TryGetValue(questId, out Coroutine routine) && routine != null)
        {
            _coroutineRunner.StopCoroutine(routine);
            _completedRoutines.Remove(questId);
        }

        if (!_completedQuestUIs.TryGetValue(questId, out QuestUI questUI)) return;

        if (questUI != null)
            Destroy(questUI.gameObject);

        _completedQuestUIs.Remove(questId);
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (_questPanel == null) return;

        _questPanel.SetActive(_activeQuestUIs.Count > 0 || _completedQuestUIs.Count > 0);
    }

    private void StopAllLingerRoutines()
    {
        if (_coroutineRunner == null) return;

        foreach (Coroutine routine in _completedRoutines.Values)
        {
            if (routine != null)
                _coroutineRunner.StopCoroutine(routine);
        }

        _completedRoutines.Clear();
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator LingerRoutine(string questId, float delay)
    {
        float end = Time.realtimeSinceStartup + delay;
        while (Time.realtimeSinceStartup < end)
            yield return null;
        RemoveCompletedLine(questId);
    }

    private QuestUI CreateQuestUI()
    {
        GameObject go = Instantiate(_questTextPrefab, _questTextContainer);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
        }

        QuestUI questUI = go.GetComponent<QuestUI>();

        if (questUI == null)
        {
            Debug.LogError("[QuestHUD] QuestTextPrefab does not have a QuestUI component on its root GameObject.");
        }

        return questUI;
    }
}