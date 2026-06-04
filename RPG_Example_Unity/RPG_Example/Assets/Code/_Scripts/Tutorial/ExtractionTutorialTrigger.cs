using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExtractionTutorialTrigger : TriggerPlayer
{
    [SerializeField] private BlockingTutorialUI _tutorialPanel;

    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider          = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_tutorialPanel == null) return;

        _triggerCollider.enabled = false;
        _tutorialPanel.Open();
    }
}
