using UnityEngine;

public class AnimatorIsAttacking : StateMachineBehaviour
{
    public bool isAttacking;
    private CharacterAnimation _character;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_character == null)
        {
            _character = animator.GetComponent<CharacterAnimation>();
        }
        
        _character.SetIsAttacking(isAttacking);
    }
}
