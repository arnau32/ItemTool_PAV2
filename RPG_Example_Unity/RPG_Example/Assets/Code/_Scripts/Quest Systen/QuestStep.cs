using UnityEngine;

public abstract class QuestStep : MonoBehaviour
{
    protected bool isFinished = false;
    protected string _questId;
    private int _stepIndex;

    public void InitializeQuestStep(string id, int stepIndex, string questStepState)
    {
        _questId = id;
        _stepIndex = stepIndex;
        if(!string.IsNullOrEmpty(questStepState))
        {
            SetQuestStepState(questStepState);
        }
    }
    protected void FinishQuestStep()
    {
        if (isFinished) return;

        isFinished = true;

        if (GameServices.TryGet<QuestService>(out var service))
        {
            service.AdvanceQuest(_questId);
        }

        Destroy(gameObject);
    }

    protected void ChangeState(string newState, string newStatus)
    {
        if (GameServices.TryGet<QuestService>(out var service))
        {
            service.NotifyStepStateChanged(_questId, _stepIndex, new QuestStepState(newState, newStatus));
        }
    }

    protected abstract void SetQuestStepState(string state);
}
