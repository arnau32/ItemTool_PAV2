using UnityEngine;

/// Companion component to NPC. Tracks heal state for the current village visit
/// and exposes ExecuteHeal() for HealAction and CanHeal for HealAvailableCondition.
/// Both are ScriptableObjects, so they access this via the static Current reference.
[RequireComponent(typeof(NPC))]
public class HealerNpc : MonoBehaviour
{
    #region Fields

    [SerializeField] private HealerNpcSO _data;

    private bool _hasHealedThisVisit;

    #endregion

    #region Properties

    public static HealerNpc Current { get; private set; }

    public bool CanHeal
    {
        get
        {
            if (_data == null || _hasHealedThisVisit) return false;
            if (!GameServices.TryGet<NpcLevelService>(out var svc)) return false;

            int level = svc.GetLevel(_data.npcId);
            return level > 0 && level <= _data.healPercentPerLevel.Length;
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        Current = this;
    }

    // Start runs after sceneLoaded, so SaveService has already reset hasHealedThisVisit
    // if the player returned from an expedition before we read the flag.
    private void Start()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            _hasHealedThisVisit = save.CurrentSave.meta.hasHealedThisVisit;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    #endregion

    #region Public API

    public void ExecuteHeal()
    {
        if (_data == null || _hasHealedThisVisit) return;
        if (!GameServices.TryGet<NpcLevelService>(out var svc)) return;

        int level = svc.GetLevel(_data.npcId);
        if (level <= 0 || level > _data.healPercentPerLevel.Length) return;

        var health = FindFirstObjectByType<PlayerHealthSystem>();
        if (health == null) return;

        float amount = health.MaxHealth * _data.healPercentPerLevel[level - 1];
        health.Heal(amount);
        _hasHealedThisVisit = true;

        if (GameServices.TryGet<SaveService>(out var save))
            save.CurrentSave.meta.hasHealedThisVisit = true;
    }

    #endregion
}
