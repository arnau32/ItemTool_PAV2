using UnityEngine;

/// <summary>
/// Quest step: interact with a specific object identified by interactionId.
/// HUD shows only "objectiveText" — no progress fraction needed.
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Interact")]
public class InteractStepSO : QuestStepSO
{
    [Header("Objective")]
    [Tooltip("Must match the interactionId set on the interactable in the scene.")]
    public string interactionId;

    protected override bool AdvanceImmediately => true;

    protected override void OnActivate(string savedState) { }

    // PushInitialStatus not overridden → uses base default → PushStatus() → no progress suffix

    public override string GetSaveState() => string.Empty;

    public override void OnInteracted(string id)
    {
        if (_isComplete) return;
        if (id != interactionId) return;

        Complete();
    }
}