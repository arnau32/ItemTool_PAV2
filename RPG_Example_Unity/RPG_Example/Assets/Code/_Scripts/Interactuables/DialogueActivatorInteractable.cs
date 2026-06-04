using UnityEngine;

public class DialogueActivatorInteractable : BaseInteractable
{
    #region Fields

    [SerializeField] private DialogueGraph _dialogueGraph;

    [Tooltip("Must match the npcId set on the NpcActivationState in the target scene.")]
    [SerializeField] private string _npcId;

    private bool _isUsed;
    private PlayerController _player;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        var entries = save.CurrentSave.npcs.entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].npcId == _npcId && entries[i].isUnlocked)
            {
                gameObject.SetActive(false);
                return;
            }
        }
    }

    #endregion

    #region IInteractable

    public override void OnInteract()
    {
        if (_isUsed) return;

        base.OnInteract();

        _player = FindFirstObjectByType<PlayerController>();
        _player?.Context?.PlayableController?.EnterDialogue();

        GameServices.Get<InputService>().OnUIOpen();

        DialogueManager.Instance.OnDialogueEnd += HandleDialogueEnded;
        DialogueManager.Instance.StartDialogue(_dialogueGraph);
    }

    #endregion

    #region Private

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnd -= HandleDialogueEnded;

        _isUsed = true;

        _player?.Context?.PlayableController?.ExitDialogue();
        _player = null;

        if (FadeTransitionSequencer.TryGetInstance(out var sequencer))
            sequencer.PlayTransitionWithAction(ApplyRescue);
        else
            ApplyRescue();
    }

    private void ApplyRescue()
    {
        NpcActivationState.SetUnlocked(_npcId, true);
        gameObject.SetActive(false);
    }

    #endregion
}
