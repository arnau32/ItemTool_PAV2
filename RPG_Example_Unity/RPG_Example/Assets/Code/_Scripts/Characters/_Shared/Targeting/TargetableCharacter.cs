using UnityEngine;

public class TargetableCharacter : MonoBehaviour, ITargetable
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private FactionComponent _factionComponent;
    [SerializeField] private IHealth _health;

    [SerializeField]private Rigidbody _rb;

    private void Awake()
    {
        if (_health != null) return;
        _health = GetComponentInParent<IHealth>();
    }

    #region ITargetable Implementation

    public Transform Transform => transform;
    public Enums.Faction Faction => _factionComponent.Faction;
    public bool IsAlive => _health.IsAlive;
    public bool IsNoisy => CheckNoisy();
    public float CurrentHealth01  => _health.CurrentHealth01;
    public float TargetWindUp01 => GetTargetWindUp();
    public float TargetRecovery01 => GetTargetRecovery();

    #endregion

    #region Internal Logic

    private bool CheckNoisy()
    {
        if(_player == null) return false;
        var mov = _player.Context.Movement;

        return mov.IsSprinting || mov.DodgeSystem.IsInDodge;
    }
    
    private float GetTargetWindUp() => _player == null ? 0f : _player.Context.ComboController.GetWindUp();

    private float GetTargetRecovery() => _player == null ? 0f : _player.Context.ComboController.GetTargetRecovery();

    public Vector3 Velocity
    {
        get
        {
            if (_rb == null) return Vector3.zero;
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            return v;
        }
    }

    #endregion

}