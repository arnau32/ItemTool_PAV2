using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class QuestNotificationUI : MonoBehaviour
{
    #region Fields

    [SerializeField] private GameObject         _panel;
    [SerializeField] private Animator           _animator;
    [SerializeField] private TextMeshProUGUI    _titleText;
    [SerializeField] private TextMeshProUGUI    _questNameText;

    [Header("Animator")]
    [SerializeField] private string _activeParam = "Active";

    [Header("Timing")]
    [SerializeField] private float _displayDuration = 2.5f;
    [SerializeField] private float _outDuration     = 0.5f;

    [Header("Labels")]
    [SerializeField] private LocalizedString _receivedLabel;
    [SerializeField] private LocalizedString _completedLabel;
    [SerializeField] private LocalizedString _newObjectiveLabel;

    private readonly Queue<(string title, string questName)> _queue = new();
    private readonly HashSet<string> _notifiedInProgress = new();
    private Coroutine              _drainRoutine;
    private QuestService           _questService;
    private MorranContractService  _morranService;
    private CoroutineRunner        _runner;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        if (_panel == null)
        {
            Debug.LogError("[QuestNotificationUI] _panel no asignado en el Inspector.", this);
            return;
        }

        _panel.SetActive(false);

        if (!GameServices.TryGet(out _questService))
        {
            Debug.LogError("[QuestNotificationUI] QuestService no encontrado.", this);
            return;
        }

        _runner = GameServices.Get<CoroutineRunner>();

        SeedAlreadyActiveQuests();

        _questService.OnQuestStateChanged += OnQuestStateChanged;
        _questService.OnStepCompleted     += OnStepCompleted;

        if (GameServices.TryGet(out _morranService))
        {
            _morranService.OnContractChanged   += OnContractAssigned;
            _morranService.OnContractCompleted += OnContractCompleted;
        }
    }

    private void OnDestroy()
    {
        if (_questService != null)
        {
            _questService.OnQuestStateChanged -= OnQuestStateChanged;
            _questService.OnStepCompleted     -= OnStepCompleted;
        }

        if (_morranService != null)
        {
            _morranService.OnContractChanged   -= OnContractAssigned;
            _morranService.OnContractCompleted -= OnContractCompleted;
        }

        if (_runner != null && _drainRoutine != null)
            _runner.StopCoroutine(_drainRoutine);
    }

    #endregion

    #region Public API

    public void Show(string title, string questName)
    {
        _queue.Enqueue((title, questName));
        if (_drainRoutine == null)
            _drainRoutine = _runner.StartCoroutine(DrainQueue());
    }

    #endregion

    #region Private

    private void SeedAlreadyActiveQuests()
    {
        foreach (var quest in _questService.GetAllQuests())
        {
            if (quest.state == Enums.QuestState.InProgress ||
                quest.state == Enums.QuestState.CanFinish)
            {
                _notifiedInProgress.Add(quest.info.id);
            }
        }
    }

    private void OnContractAssigned(MorranContractSO contract)
    {
        if (contract == null) return;
        Show(_receivedLabel.GetLocalizedString(), contract.contractName.GetLocalizedString());
    }

    private void OnContractCompleted(MorranContractSO contract)
    {
        if (contract == null) return;
        Show(_completedLabel.GetLocalizedString(), contract.contractName.GetLocalizedString());
    }

    private void OnQuestStateChanged(Quest quest)
    {
        switch (quest.state)
        {
            case Enums.QuestState.InProgress:
                if (_notifiedInProgress.Add(quest.info.id))
                    Show(_receivedLabel.GetLocalizedString(), quest.info.qName.GetLocalizedString());
                break;
            case Enums.QuestState.Finished:
                _notifiedInProgress.Remove(quest.info.id);
                Show(_completedLabel.GetLocalizedString(), quest.info.qName.GetLocalizedString());
                break;
        }
    }

    private void OnStepCompleted(Quest quest, QuestStepSO step, bool isLastStep)
    {
        if (isLastStep) return;
        Show(_newObjectiveLabel.GetLocalizedString(), quest.info.qName.GetLocalizedString());
    }

    private IEnumerator DrainQueue()
    {
        while (_queue.Count > 0)
        {
            // Hold while dialogue is open.
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
                yield return null;

            if (_queue.Count == 0) break;

            var (title, questName) = _queue.Dequeue();
            yield return ShowNotification(title, questName);
        }

        _drainRoutine = null;
    }

    private IEnumerator ShowNotification(string title, string questName)
    {
        _titleText.text     = title;
        _questNameText.text = questName;

        _panel.SetActive(true);
        _animator.SetBool(_activeParam, true);

        yield return new WaitForSecondsRealtime(_displayDuration);

        _animator.SetBool(_activeParam, false);

        yield return new WaitForSecondsRealtime(_outDuration);

        _panel.SetActive(false);
    }

    #endregion
}