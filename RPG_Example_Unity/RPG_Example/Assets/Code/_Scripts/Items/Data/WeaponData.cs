using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Items/Weapon")]
public class WeaponData : EquipableItemData
{
    public RuntimeAnimatorController animatorOverride;
    public Enums.WeaponHandType handType;
    public Enums.WeaponFamily familyType;

    [Tooltip("Other hand weapon")] public GameObject prefabVariant;

    public List<Combo> combos = new List<Combo>();

    public DodgeDataSet dodgeSet;
    public ParryData parry;

    public AttackData weaponSkill;
    public float skillScoreNeeded = 6f;

    [Header("Balance")]
    [Tooltip("Tier 1 = base, Tier 2 = mid, Tier 3 = advanced, Tier 4 = endgame.")]
    [Range(1, 4)] public int weaponTier = 1;

    [Header("Enemy Scaling")]
    [Tooltip("Multiplies the enemy's Attack stat when this weapon is equipped. 1 = no change, 1.5 = +50%.")]
    [Range(0f, 5f)] public float enemyDamageMultiplier = 1f;
}
