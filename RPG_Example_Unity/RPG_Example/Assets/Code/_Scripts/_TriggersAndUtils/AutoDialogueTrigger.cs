using UnityEngine;

// Fires a DialogueGraph automatically when the player enters the trigger zone.
// Uses the NpcsSaveData list to remember that it already fired — won't repeat across sessions.
// If _triggerId is left empty the dialogue fires every session (useful for testing).
public class AutoDialogueTrigger : TriggerPlayer
{
    #region Fields

    [SerializeField] private DialogueGraph _dialogueGraph;

    [Tooltip("Unique ID stored in the save file so this trigger only fires once. Leave empty to fire every session.")]
    [SerializeField] private string _triggerId;

    private bool _fired;
    private PlayerController _cachedPlayer;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        if (string.IsNullOrEmpty(_triggerId)) return;
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        var entries = save.CurrentSave.npcs.entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].npcId == _triggerId && entries[i].isUnlocked)
            {
                _fired = true;
                return;
            }
        }
    }

    #endregion

    #region TriggerPlayer

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_fired || _dialogueGraph == null) return;

        _fired = true;

        if (!string.IsNullOrEmpty(_triggerId))
            NpcActivationState.SetUnlocked(_triggerId, true);

        _cachedPlayer = other.GetComponentInParent<PlayerController>();
        _cachedPlayer?.Context?.PlayableController?.EnterDialogue();

        GameServices.Get<InputService>().OnUIOpen();
        DialogueManager.Instance.OnDialogueEnd += HandleDialogueEnded;
        DialogueManager.Instance.StartDialogue(_dialogueGraph);
    }

    #endregion

    #region Private

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnd -= HandleDialogueEnded;
        _cachedPlayer?.Context?.PlayableController?.ExitDialogue();
        _cachedPlayer = null;
    }

    #endregion
}
