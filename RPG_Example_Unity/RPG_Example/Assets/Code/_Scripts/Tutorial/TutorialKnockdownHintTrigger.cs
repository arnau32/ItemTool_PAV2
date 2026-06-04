using UnityEngine;

public class TutorialKnockdownHintTrigger : MonoBehaviour
{
    [SerializeField] private TutorialKnockdownHint _hint;

    #region Unity Callbacks

    private void Start()
    {
        if (GameServices.TryGet<SaveService>(out var save) &&
            save.CurrentSave.meta.knockdownHintShown)
        {
            enabled = false;
            return;
        }

        PlayerLifeController.OnPlayerKnockdown += OnKnockdown;
    }

    private void OnDestroy()
    {
        PlayerLifeController.OnPlayerKnockdown -= OnKnockdown;
    }

    #endregion

    #region Helpers

    private void OnKnockdown()
    {
        PlayerLifeController.OnPlayerKnockdown -= OnKnockdown;

        if (GameServices.TryGet<SaveService>(out var save))
        {
            save.CurrentSave.meta.knockdownHintShown = true;
            save.SaveImmediate();
        }

        if (_hint != null)
            _hint.enabled = true;

        enabled = false;
    }

    #endregion
}
