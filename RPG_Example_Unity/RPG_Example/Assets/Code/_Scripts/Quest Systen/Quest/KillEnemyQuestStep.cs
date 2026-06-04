using UnityEngine;

/// <summary>
/// Quest step: kill N enemies of a specific EnemyDefinition type.
/// Progress is shown as "objectiveText (killed/required)".
/// </summary>
[CreateAssetMenu(menuName = "Quests/Steps/Kill Enemy")]
public class KillEnemyStepSO : QuestStepSO
{
    [Header("Objective")]
    public EnemyDefinition targetEnemy;
    [Min(1)] public int requiredKills = 1;

    // Runtime counter — not serialized, restored from savedState string.
    private int _currentKills;

    protected override void OnActivate(string savedState)
    {
        _currentKills = int.TryParse(savedState, out int saved) ? saved : 0;
    }

    protected override void PushInitialStatus() => PushProgress(_currentKills, requiredKills);

    public override string GetSaveState() => _currentKills.ToString();

    // Handles the crash-during-extraction edge case: save shows InProgress with
    // _currentKills == requiredKills. On next Village load, complete immediately.
    public override void CheckCompletionOnActivate()
    {
        if (_currentKills >= requiredKills)
            Complete();
    }

    public override void OnEnemyKilled(EnemyDefinition definition)
    {
        if (_isComplete) return;
        if (definition == null) return;
        if (targetEnemy != null && definition != targetEnemy) return;

        _currentKills++;
        PushProgress(_currentKills, requiredKills);

        if (_currentKills >= requiredKills)
            Complete();
    }
}