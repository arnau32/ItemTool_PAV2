using UnityEngine;
using UnityEngine.Events;
using FeedbacksNagu;

public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("If set, reports this ID to QuestService on interact so InteractStepSO can react.")]
    [SerializeField] private string _interactionId;

    [SerializeField] private FeedbackContainer _onInteractFeedback;

    public UnityEvent OnInteractEvents;

    public virtual void OnInteract()
    {
        OnInteractEvents?.Invoke();
        _onInteractFeedback.PlayFeedbacks(gameObject);

        if (!string.IsNullOrEmpty(_interactionId) && GameServices.TryGet<QuestService>(out var qs))
            qs.ReportInteraction(_interactionId);
    }
}
