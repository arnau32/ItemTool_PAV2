using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quest step: interact with all listed interactable IDs in any order.
/// HUD shows "objectiveText (visited/required)".
/// Each ID is counted only once regardless of how many times the player interacts.
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Interact Multiple")]
public class InteractMultipleStepSO : QuestStepSO
{
    #region Fields

    [Header("Objective")]
    [Tooltip("IDs that must be interacted with. Must match interactionId on each interactable in the scene.")]
    public List<string> interactionIds = new List<string>();

    private readonly HashSet<string> _visited = new HashSet<string>();

    #endregion

    #region QuestStepSO

    protected override bool AdvanceImmediately => true;

    protected override void OnActivate(string savedState)
    {
        _visited.Clear();

        if (string.IsNullOrEmpty(savedState)) return;

        var parts = savedState.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
                _visited.Add(parts[i]);
        }
    }

    protected override void PushInitialStatus() => PushProgress(_visited.Count, interactionIds.Count);

    public override string GetSaveState() => string.Join(",", _visited);

    public override void CheckCompletionOnActivate()
    {
        if (!GameServices.TryGet<QuestService>(out var qs)) return;

        bool changed = false;
        for (int i = 0; i < interactionIds.Count; i++)
        {
            string id = interactionIds[i];
            if (_visited.Contains(id)) continue;
            if (!qs.HasInteractionBeenReported(id)) continue;
            _visited.Add(id);
            changed = true;
        }

        if (!changed) return;
        PushProgress(_visited.Count, interactionIds.Count);

        if (_visited.Count >= interactionIds.Count)
            Complete();
    }

    public override void OnInteracted(string id)
    {
        if (_isComplete) return;
        if (!interactionIds.Contains(id)) return;
        if (_visited.Contains(id)) return;

        _visited.Add(id);
        PushProgress(_visited.Count, interactionIds.Count);

        if (_visited.Count >= interactionIds.Count)
            Complete();
    }

    #endregion
}
