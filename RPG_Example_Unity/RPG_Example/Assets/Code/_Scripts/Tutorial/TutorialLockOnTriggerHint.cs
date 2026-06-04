using UnityEngine;

public class TutorialLockOnTriggerHint : TriggerPlayer
{
    [SerializeField] private TutorialLockOnHint _hint;

    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_hint == null) return;

        _triggerCollider.enabled = false;
        _hint.enabled = true;
    }
}
