using UnityEngine;

public class SkillTreeInteractable : BaseInteractable
{
    #region Fields

    [SerializeField] private SkillTreeUI _ui;

    private PlayerUpgradeService _upgradeService;
    private InputService _inputService;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _upgradeService = FindFirstObjectByType<PlayerUpgradeService>();
        _inputService = GameServices.Get<InputService>();
    }

    #endregion

    #region IInteractable

    public override void OnInteract()
    {
        base.OnInteract();

        if (_ui == null)
        {
            Debug.LogWarning("[SkillTreeInteractable] SkillTreeUI reference not assigned.", this);
            return;
        }

        if (_upgradeService == null)
        {
            Debug.LogWarning("[SkillTreeInteractable] PlayerUpgradeService not found.", this);
            return;
        }

        _inputService.OnUIOpen();
        _ui.Open(_upgradeService);
    }

    #endregion
}
