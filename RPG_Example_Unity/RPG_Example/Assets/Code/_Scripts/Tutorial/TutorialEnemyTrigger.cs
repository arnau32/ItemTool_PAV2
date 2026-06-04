using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TutorialEnemyTrigger : TriggerPlayer
{
    [SerializeField] private EnemyBase _enemy;
    [SerializeField] private DialogueGraph _dialogueGraph;
    [SerializeField] private TargetableCharacter _playerTarget;
    [SerializeField] private TutorialCombatHint _combatHint;
    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;
    }

    private void Start()
    {
        _enemy.Context.Perception.DisablePerception();
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_playerTarget == null) return;

        _triggerCollider.enabled = false;

        GameServices.Get<InputService>().OnUIOpen();
        DialogueManager.Instance.OnDialogueEnd += OnDialogueEnd;
        DialogueManager.Instance.StartDialogue(_dialogueGraph);
    }

    private void OnDialogueEnd()
    {
        DialogueManager.Instance.OnDialogueEnd -= OnDialogueEnd;
        _enemy.ForceEnterCombat(_playerTarget);
        if (_combatHint != null) _combatHint.enabled = true;
    }
}
