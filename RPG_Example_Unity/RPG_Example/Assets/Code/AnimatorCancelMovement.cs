using UnityEngine;

public class AnimatorCancelMovement : StateMachineBehaviour
{
    public bool cancelMovement;
    private static readonly int CancelMovement = Animator.StringToHash("CancelMovement");

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(CancelMovement, cancelMovement);
    }
}
