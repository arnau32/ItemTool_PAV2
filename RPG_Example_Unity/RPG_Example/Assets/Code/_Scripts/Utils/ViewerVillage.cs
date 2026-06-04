using FeedbacksNagu;
using UnityEngine;

public class ViewerVillage : TriggerPlayer
{
    public FeedbackContainer activeViewer;
    public FeedbackContainer deactiveViewer;

    private PlayerFeedbacksController _playerFeedbacks;

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        base.OnPlayerTriggerEnter(other);

        _playerFeedbacks = other.GetComponentInParent<PlayerFeedbacksController>();
        _playerFeedbacks?.SetViewerMode(true);

        activeViewer.PlayFeedbacks(gameObject);
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        base.OnPlayerTriggerExit(other);

        _playerFeedbacks?.SetViewerMode(false);
        _playerFeedbacks = null;

        deactiveViewer.PlayFeedbacks(gameObject);
    }
}
