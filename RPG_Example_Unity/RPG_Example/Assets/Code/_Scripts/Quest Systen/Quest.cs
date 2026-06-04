using UnityEngine;

/// <summary>
/// Runtime representation of a quest instance.
/// Pure data/logic — no MonoBehaviour, no scene dependencies.
/// </summary>
public class Quest
{
    public QuestInfoSO info;
    public Enums.QuestState state;

    private int _currentStepIndex;
    private QuestStepState[] _stepStates;

    // Reference to the currently active step SO (null if no step is active).
    // NOT serialized — rebuilt on load via ActivateCurrentStep().
    private QuestStepSO _activeStep;

    // ── Constructors ──────────────────────────────────────────────────────────

    public Quest(QuestInfoSO data)
    {
        info = data;
        state = Enums.QuestState.RequirementNotMet;
        _currentStepIndex = 0;
        _stepStates = new QuestStepState[data.questSteps.Count];

        for (int i = 0; i < _stepStates.Length; i++)
            _stepStates[i] = new QuestStepState();
    }

    public Quest(QuestInfoSO data, Enums.QuestState savedState, int stepIndex, QuestStepState[] stepStates)
    {
        info = data;
        state = savedState;
        _currentStepIndex = stepIndex;
        _stepStates = stepStates;

        if (stepStates.Length != data.questSteps.Count)
            Debug.LogWarning($"[Quest] Step count mismatch for '{info.id}'. Save may be out of sync.");
    }

    // ── Step management ───────────────────────────────────────────────────────

    public bool CurrentStepExists() => _currentStepIndex < info.questSteps.Count;

    /// <summary>
    /// Returns true when there is at least one more step after the current one.
    /// Used by QuestService.AdvanceQuest to determine whether OnStepCompleted
    /// should signal isLastStep = true to the HUD.
    /// </summary>
    public bool HasNextStep() => _currentStepIndex + 1 < info.questSteps.Count;

    /// <summary>
    /// Returns the currently active QuestStepSO reference without advancing the index.
    /// Used by QuestService.AdvanceQuest to capture the completed step before moving on,
    /// so OnStepCompleted can pass the finished step's data to the HUD.
    /// </summary>
    public QuestStepSO GetCurrentStepSO()
    {
        if (!CurrentStepExists()) return null;
        return info.questSteps[_currentStepIndex];
    }

    /// <summary>
    /// Activates the current step SO with the saved state string.
    /// Called by QuestService when a quest starts or resumes after a scene load.
    /// </summary>
    public void ActivateCurrentStep()
    {
        if (!CurrentStepExists()) return;

        _activeStep = info.questSteps[_currentStepIndex];

        string saved = (_currentStepIndex < _stepStates.Length)
            ? _stepStates[_currentStepIndex].state
            : string.Empty;

        _activeStep.Activate(info.id, _currentStepIndex, saved);
    }

    public void MoveToNextStep()
    {
        _activeStep = null;
        _currentStepIndex++;
    }

    public void RewindToStep(int stepIndex)
    {
        _activeStep = null;
        _currentStepIndex = stepIndex;
    }

    // Asks the active step whether it is already satisfied (e.g. items already
    // in inventory). Called by QuestService after activation when in safe zone.
    public void CheckCurrentStepCompletion() => _activeStep?.CheckCompletionOnActivate();

    // ── Active step routing — called by QuestService.Report* ─────────────────

    public void RouteEnemyKilled(EnemyDefinition def) => _activeStep?.OnEnemyKilled(def);
    public void RouteZoneVisited(string zoneId) => _activeStep?.OnZoneVisited(zoneId);
    public void RouteItemCollected(ItemData item, int qty) => _activeStep?.OnItemCollected(item, qty);
    public void RouteInteraction(string id) => _activeStep?.OnInteracted(id);
    public void RouteAuraDustChanged(int newAmount) => _activeStep?.OnAuraDustChanged(newAmount);

    // ── Step state ────────────────────────────────────────────────────────────

    public void StoreStepState(QuestStepState stepState, int index)
    {
        if (index >= _stepStates.Length)
        {
            Debug.LogWarning($"[Quest] StoreStepState: index {index} out of range for '{info.id}'.");
            return;
        }

        _stepStates[index].state = stepState.state;
        _stepStates[index].status = stepState.status;
    }

    public string GetCurrentStepStatus()
    {
        if (!CurrentStepExists()) return string.Empty;
        return _stepStates[_currentStepIndex].status;
    }

    /// <summary>
    /// Returns the objective description text from the current step SO.
    /// Empty string if no step is active or questSteps list is out of range.
    /// </summary>
    public string GetCurrentStepObjective()
    {
        if (!CurrentStepExists()) return string.Empty;
        var step = info.questSteps[_currentStepIndex];
        return step != null ? step.objectiveText.GetLocalizedString() : string.Empty;
    }

    /// <summary>
    /// Builds the full HUD line: "Objective text (status)".
    /// If status is empty, returns just the objective text.
    /// </summary>
    public string GetCurrentStepDisplayText()
    {
        if (!CurrentStepExists())
            return info.canFinishText.IsEmpty ? string.Empty : info.canFinishText.GetLocalizedString();

        string objective = GetCurrentStepObjective();
        string status = GetCurrentStepStatus();

        if (string.IsNullOrEmpty(objective)) return status;
        if (string.IsNullOrEmpty(status)) return objective;
        return $"{objective} ({status})";
    }

    public QuestData GetQuestData() => new QuestData(state, _currentStepIndex, _stepStates);
}