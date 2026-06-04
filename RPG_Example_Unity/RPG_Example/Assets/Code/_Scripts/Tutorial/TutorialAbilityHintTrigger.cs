using UnityEngine;

public class TutorialAbilityHintTrigger : MonoBehaviour
{
    [SerializeField] private TutorialAbilityHint _hint;

    #region Unity Callbacks

    private void Start()
    {
        if (GameServices.TryGet<SaveService>(out var save) &&
            save.CurrentSave.meta.abilityReadyHintShown)
        {
            enabled = false;
            return;
        }

        AbilityScoreSystem.OnAbilityReady += OnAbilityReady;
    }

    private void OnDestroy()
    {
        AbilityScoreSystem.OnAbilityReady -= OnAbilityReady;
    }

    #endregion

    #region Helpers

    private void OnAbilityReady()
    {
        AbilityScoreSystem.OnAbilityReady -= OnAbilityReady;

        if (GameServices.TryGet<SaveService>(out var save))
        {
            save.CurrentSave.meta.abilityReadyHintShown = true;
            save.SaveImmediate();
        }

        if (_hint != null)
            _hint.enabled = true;

        enabled = false;
    }

    #endregion
}
