using UnityEngine;

/// Scene-placed marker that enemies can walk to and stand at during idle routines.
/// Place on props like campfires, barrels, doorways, or overlook points.
public class InteractPoint : MonoBehaviour
{
    [Tooltip("Facing direction the enemy adopts while standing at this point. Leave zero to keep the agent's natural facing.")]
    [SerializeField] private Vector3 _facingDirection;

    [Tooltip("If true, only one enemy occupies this point at a time.")]
    [SerializeField] private bool _exclusive = true;

    private bool _occupied;

    public bool IsAvailable => !_exclusive || !_occupied;
    public Vector3 Position  => transform.position;

    /// World-space facing override. Returns transform.forward if not configured.
    public Vector3 FacingWorld => _facingDirection.sqrMagnitude > 0.001f ? transform.TransformDirection(_facingDirection.normalized) : transform.forward;

    public void Occupy()   => _occupied = true;
    public void Release()  => _occupied = false;

    private void OnDisable() => _occupied = false;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + FacingWorld * 0.6f);
    }
#endif
}