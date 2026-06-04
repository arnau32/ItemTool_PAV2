using UnityEngine;

[CreateAssetMenu(fileName = "DodgeSet", menuName = "Combat/Dodge Set", order = 0)]
public class DodgeDataSet : ScriptableObject
{
    [Header("Shared Properties")] 
    public float staminaCost;
    public float groundPointOffset;
        
    [Header("Free Dodge (no lock-on)")]
    public DodgeData freeDodge;
    
    [Header("Locked Directional Dodges")]
    [Tooltip("Dodge Front")] public DodgeData forwardDodge;
    [Tooltip("Dodge Back")] public DodgeData backwardDodge;
    [Tooltip("Dodge Left")] public DodgeData leftDodge;
    [Tooltip("Dodge Right")] public DodgeData rightDodge;
        
    public DodgeData GetDodgeForDirection(bool isLockedOn, Vector2 cardinal)
    {
        if (!isLockedOn) return freeDodge;
        if (cardinal == Vector2.up) return forwardDodge;
        if (cardinal == Vector2.down) return backwardDodge;
        if (cardinal == Vector2.left) return leftDodge;
        if (cardinal == Vector2.right) return rightDodge;
        
        return forwardDodge;
    }
        
}
