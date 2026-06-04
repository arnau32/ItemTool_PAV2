using UnityEngine;

/// <summary>
/// Quest step: visit a specific zone identified by a string ID.
/// HUD shows only "objectiveText" — no progress fraction needed.
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Visit Zone")]
public class VisitZoneStepSO : QuestStepSO
{
    [Header("Objective")]
    [Tooltip("Must exactly match the zoneId on the VisitZoneTrigger in the Level scene.")]
    public string zoneId;

    protected override bool AdvanceImmediately => true;

    protected override void OnActivate(string savedState) { }

    // PushInitialStatus not overridden → uses base default → PushStatus() → no progress suffix

    public override string GetSaveState() => string.Empty;

    public override void OnZoneVisited(string id)
    {
        if (_isComplete) return;
        if (id != zoneId) return;

        Complete();
    }
}