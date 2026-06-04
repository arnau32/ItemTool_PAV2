using System;
using UnityEngine;

/// <summary>
/// World-space icon above an NPC or location that reflects quest availability.
/// Subscribe to QuestService events to update icon state.
/// </summary>
public class QuestIcon : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] private string _questId;
    [SerializeField] private bool _isStartPoint;
    [SerializeField] private bool _isFinishPoint;

    [Header("Icons")]
    [SerializeField] private GameObject requirementsNotMetIcon;
    [SerializeField] private GameObject canStartIcon;
    [SerializeField] private GameObject inProgressIcon;
    [SerializeField] private GameObject canFinishIcon;

    private QuestService _questService;

    private void Start()
    {
        if (!GameServices.TryGet<QuestService>(out _questService))
        {
            Debug.LogError("[QuestIcon] QuestService not found.");
            return;
        }

        _questService.OnQuestStateChanged += HandleQuestStateChanged;

        // Apply current state immediately
        var quest = _questService.GetQuestById(_questId);
        if (quest != null)
            ApplyState(quest.state);
    }

    private void OnDestroy()
    {
        if (_questService != null)
            _questService.OnQuestStateChanged -= HandleQuestStateChanged;
    }

    private void HandleQuestStateChanged(Quest quest)
    {
        if (quest.info.id != _questId) return;

        ApplyState(quest.state);
    }

    private void ApplyState(Enums.QuestState newState)
    {
        SetAllInactive();

        switch (newState)
        {
            case Enums.QuestState.RequirementNotMet:
                if (_isStartPoint) requirementsNotMetIcon.SetActive(true);
                break;
            case Enums.QuestState.CanStart:
                if (_isStartPoint) canStartIcon.SetActive(true);
                break;
            case Enums.QuestState.InProgress:
                if (_isFinishPoint) inProgressIcon.SetActive(true);
                break;
            case Enums.QuestState.CanFinish:
                if (_isFinishPoint) canFinishIcon.SetActive(true);
                break;
            case Enums.QuestState.Finished:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    private void SetAllInactive()
    {
        requirementsNotMetIcon.SetActive(false);
        canStartIcon.SetActive(false);
        inProgressIcon.SetActive(false);
        canFinishIcon.SetActive(false);
    }
}