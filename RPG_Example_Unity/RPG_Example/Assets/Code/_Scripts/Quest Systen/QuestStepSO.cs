using UnityEngine;
using UnityEngine.Localization;

public abstract class QuestStepSO : ScriptableObject
{
    [Header("Display")] [Tooltip("Text shown in the HUD for this step. Steps with counters append (current/required) via PushProgress().")]
    public LocalizedString objectiveText;

    // Runtime quest context — set by QuestService on activation, NOT serialized.
    [System.NonSerialized] protected string _questId;
    [System.NonSerialized] protected int _stepIndex;
    [System.NonSerialized] protected bool _isComplete;

    public bool IsComplete => _isComplete;

    // ── Lifecycle (called by QuestService) ────────────────────────────────────

    /// <summary>
    /// Called when this step becomes the active step for its quest.
    /// Calls OnActivate() for subclass-specific logic, then pushes the
    /// initial HUD text. Subclasses must NOT call PushStatus/PushProgress
    /// from OnActivate — Activate() does the initial push after OnActivate returns.
    /// </summary>
    public void Activate(string questId, int stepIndex, string savedState)
    {
        _questId = questId;
        _stepIndex = stepIndex;
        _isComplete = false;

        OnActivate(savedState);

        // Initial push after OnActivate so subclasses can set their counters
        // (e.g. CollectItemStepSO restores count from inventory) before the
        // HUD text is built. PushInitialStatus() lets each subclass decide
        // whether to show just objectiveText or objectiveText + progress.
        PushInitialStatus();
    }

    /// <summary>Called when the quest is saved — return a short serializable string.</summary>
    public abstract string GetSaveState();

    // ── Event entry points (called by QuestService.Report*) ──────────────────

    public virtual void OnEnemyKilled(EnemyDefinition definition)
    {
    }

    public virtual void OnZoneVisited(string zoneId)
    {
    }

    public virtual void OnItemCollected(ItemData item, int quantity)
    {
    }

    public virtual void OnInteracted(string id)
    {
    }

    public virtual void OnAuraDustChanged(int newAmount)
    {
    }

    /// <summary>
    /// Called by QuestService after step activation when in safe zone (Village).
    /// Override in steps that can be satisfied by pre-existing state
    /// (e.g. items already in inventory, kills already accumulated in save).
    /// Default: no-op. Do NOT override in visit/interact steps — they have no
    /// persistent objective state that survives between scenes.
    /// </summary>
    public virtual void CheckCompletionOnActivate() { }

    // ── Protected API for subclasses ──────────────────────────────────────────

    /// <summary>
    /// Subclass initialization. Restore runtime counters from savedState if not empty.
    /// Do NOT call PushStatus/PushProgress here — Activate() will call PushInitialStatus()
    /// after this returns.
    /// </summary>
    protected abstract void OnActivate(string savedState);

    /// <summary>
    /// Override to push the correct initial HUD text after Activate().
    /// Default: pushes objectiveText with no progress (visit, interact steps).
    /// Steps with counters override this to push "objectiveText (0/N)".
    /// </summary>
    protected virtual void PushInitialStatus() => PushStatus();

    /// <summary>
    /// Pushes objectiveText to the HUD with no progress suffix.
    /// Use for one-shot steps (visit zone, interact).
    /// HUD shows: "objectiveText"
    /// </summary>
    protected void PushStatus()
    {
        NotifyStatus(string.Empty);
    }

    /// <summary>
    /// Pushes objectiveText + a progress fraction to the HUD.
    /// HUD shows: "objectiveText (current/required)"
    /// </summary>
    protected void PushProgress(int current, int required)
    {
        NotifyStatus($"{current}/{required}");
    }

    // Steps that override this to return true will always advance immediately,
    // bypassing the extraction-deferral mechanism. Safe for steps that have no
    // inventory payload that could be lost on death (visit, interact).
    protected virtual bool AdvanceImmediately => false;

    protected void Complete()
    {
        if (_isComplete) return;
        _isComplete = true;

        if (!GameServices.TryGet<QuestService>(out var qs)) return;

        if (qs.IsInSafeZone || AdvanceImmediately)
            qs.AdvanceQuest(_questId);
        else
            qs.RegisterPendingAdvance(_questId);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void NotifyStatus(string status)
    {
        if (GameServices.TryGet<QuestService>(out var qs))
            qs.NotifyStepStateChanged(_questId, _stepIndex, new QuestStepState(GetSaveState(), status));
    }
}