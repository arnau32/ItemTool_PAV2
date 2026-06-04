using UnityEngine;

public class NPC_QuestGiver : MonoBehaviour, IInteractable
{
    public enum DirectMode { StartOnly, FinishOnly, StartAndFinish }

    [Header("Dialogue (optional)")]
    public DialogueGraph dialogueGraph;

    [Header("Direct Quest")]
    [SerializeField] private string     _questId;
    [SerializeField] private DirectMode _directMode = DirectMode.StartAndFinish;

    [Header("Quest Interaction Tracking")]
    [Tooltip("If set, reports this ID to QuestService on interact. " +
             "Must match the interactionId listed in InteractMultipleStepSO.")]
    [SerializeField] private string _interactionId;

    private Animator _animator;

    private static readonly int HashDialogueOpen  = Animator.StringToHash("OnDialogueOpen");
    private static readonly int HashDialogueClose = Animator.StringToHash("OnDialogueClose");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void OnInteract()
    {
        bool hasDirect   = !string.IsNullOrEmpty(_questId);
        bool hasDialogue = dialogueGraph != null;

        if (!string.IsNullOrEmpty(_interactionId) && GameServices.TryGet<QuestService>(out var qs))
            qs.ReportInteraction(_interactionId);

        if (hasDirect)
            HandleDirectQuest();

        if (!hasDialogue) return;

        GameServices.Get<InputService>().OnUIOpen();
        _animator?.SetTrigger(HashDialogueOpen);
        DialogueManager.Instance.OnDialogueEnd += HandleDialogueEnded;
        DialogueManager.Instance.StartDialogue(dialogueGraph);
    }

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnd -= HandleDialogueEnded;
        _animator?.SetTrigger(HashDialogueClose);
    }

    private void HandleDirectQuest()
    {
        if (!GameServices.TryGet<QuestService>(out var qs)) return;

        var quest = qs.GetQuestById(_questId);
        if (quest == null) return;

        switch (_directMode)
        {
            case DirectMode.StartOnly:
                if (quest.state == Enums.QuestState.CanStart)   qs.StartQuest(_questId);
                break;
            case DirectMode.FinishOnly:
                if (quest.state == Enums.QuestState.CanFinish)  qs.FinishQuest(_questId);
                break;
            case DirectMode.StartAndFinish:
                if (quest.state == Enums.QuestState.CanStart)   qs.StartQuest(_questId);
                else if (quest.state == Enums.QuestState.CanFinish) qs.FinishQuest(_questId);
                break;
        }
    }
}