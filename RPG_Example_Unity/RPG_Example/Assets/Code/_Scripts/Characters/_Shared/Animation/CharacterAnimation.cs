using System;
using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    #region Fields

    [SerializeField] protected Animator _animator;
    protected bool _isAttacking = false;
    protected bool _cancelMovement = false;
    public event Action<bool> OnAttacking;
    
    #endregion

    #region Properties
    
    public Animator Animator => _animator;
    public bool IsAttacking => _isAttacking;

    #endregion
    
    protected virtual void Awake()
    {
        if(_animator == null) _animator = GetComponent<Animator>();
        _cancelMovement = false;
    }

    #region Public API

    public virtual void PlayTargetAnimation(string targetAnimation, float timeToFade)
    {
        _animator.CrossFade(targetAnimation, timeToFade);
    }
    public void SetCancelMovement(bool v)
    {
        if (_cancelMovement == v) return;

        _cancelMovement = v;
        _animator.SetBool(PlayerAnimHashes.CancelMovementWindow, v);
    }
    public void PlayTargetAnimation(int stateHash, float crossFade, int layer)
    {
        if (stateHash == 0) return;

        if (crossFade <= 0f)
        {
            _animator.Play(stateHash, layer, 0f);
            return;
        }
        
        _animator.CrossFade(stateHash, crossFade, layer);
    }

    public void SetIsAttacking(bool currentAttacking)
    {
        if (_isAttacking == currentAttacking) return;
        
        _isAttacking = currentAttacking;

        OnAttacking?.Invoke(_isAttacking);
    }

    public void UpdateRuntimeAnimationController(RuntimeAnimatorController controllerOverride) => _animator.runtimeAnimatorController = controllerOverride;

    #endregion
}
