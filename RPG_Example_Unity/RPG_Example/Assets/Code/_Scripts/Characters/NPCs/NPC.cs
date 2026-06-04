using UnityEngine;
using FeedbacksNagu;

public class NPC : MonoBehaviour, IInteractable
{
    [Tooltip("The dialogue graph to open on interact.")]
    public DialogueGraph dialogueGraph;

    [SerializeField] private FeedbackContainer onInteract;
    [SerializeField] private FeedbackContainer onDialogueClose;

    private Animator _animator;

    private static readonly int HashDialogueOpen  = Animator.StringToHash("OnDialogueOpen");
    private static readonly int HashDialogueClose = Animator.StringToHash("OnDialogueClose");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void OnInteract()
    {
        if (dialogueGraph == null)
        {
            Debug.LogWarning($"[NPC] {gameObject.name} has no DialogueGraph assigned.", this);
            return;
        }
        // [ASSUMPTION] PlayerController is fetched via FindObjectOfType once per interaction.
        //  acceptable because OnInteract is a one-shot user action, not a hot path.
        // If a PlayerRegistry service is added to GameServices later, replace this lookup.
        var player = FindFirstObjectByType<PlayerController>();
        player?.Context?.PlayableController?.EnterDialogue();

        GameServices.Get<InputService>().OnUIOpen();

        _animator?.SetTrigger(HashDialogueOpen);
        onInteract.PlayFeedbacks(gameObject);
        DialogueManager.Instance.OnDialogueEnd += HandleDialogueEnded;
        DialogueManager.Instance.StartDialogue(dialogueGraph);
    }

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnd -= HandleDialogueEnded;

        var player = FindFirstObjectByType<PlayerController>();
        player?.Context?.PlayableController?.ExitDialogue();
        _animator?.SetTrigger(HashDialogueClose);
        onDialogueClose.PlayFeedbacks(gameObject);
    }
}