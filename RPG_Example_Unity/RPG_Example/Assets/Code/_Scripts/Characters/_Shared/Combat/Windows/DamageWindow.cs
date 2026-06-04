
[System.Serializable]
public class DamageWindow
{
    public WindowEvent window;
    public Enums.WeaponHand weaponHand;
    public Enums.ColliderSlot slot;
    
    public bool allowMultiHit;
    public float perTargetCooldown;
    public int maxHitsPerTarget = 1;
}
