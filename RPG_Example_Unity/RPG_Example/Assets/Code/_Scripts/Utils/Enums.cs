using UnityEngine;

public class Enums
{
    #region Items & Inventory
    public enum ItemType { Equipable, Consumable, Crafting, Collectable }
    public enum ItemRarity { Common, Rare, Epic, Legendary }
    public enum EquipSlot { Backpack, Helmet, ChestArmor, Weapon, LowerArmor }
    public enum WeaponHandType { Single, DualSymmetric, DualAsymetric }
    public enum WeaponFamily { Hands, Sword, Warrior, Spear, GreatSword, Dagger }
    
    public enum DeathPenalty { KeepAll, KeepOnEnter, LoseAll }
    
    public enum LootEntryType { Item, LootTable }
    public enum EquipableRollMode { FixedRarityRandomStats, RandomRarityRandomStats, FixedRarityFixedStats }
    public enum LootEntryEquipableOverrideMode { None, FixedRarityRandomStats, RandomRarityRandomStats, FixedRarityFixedStats }
    
    #endregion
    
    public enum BuffApplicationMode { ModifyStat, InstantHeal, HealOverTime, ModifyStaminaRegen }

    public enum EnemyCategory { Normal, Fast, Tank, Elite, Boss }
    
    public enum StatType { Health, Defense, Stamina, Speed, Attack, Weight }
    public enum AttackInputs { LightInput, HeavyInput, Skill, Run_Attack }
    
    public enum QuestState { RequirementNotMet, CanStart, InProgress, CanFinish, Finished }

    #region Combat System
    public enum HitType { Normal, Knockback, Knockdown }
    public enum WeaponHand { Right = 0, Left = 1 }

    public enum ColliderSlot
    {
        MainRight = 0,
        MainLeft = 1,
        SecondaryRight = 2,
        SecondaryLeft = 3,
        Special = 4,
        Parry = 5
    }
    
    public enum ParryQuality { Normal, Perfect, None }
    #endregion

    #region Enemies
    public enum Faction { Player, Skeleton, SkeletonRanged }
    public enum CombatTemperament { SuperDefensive, Defensive, Normal, Aggressive, SuperAgressive }
    public enum ActionCategory { Attack, Movement, Defense, Disengage, Flank, Bait, Special }
    
    public enum AlertLevel
    {
        Unaware    = 0,  // Patrolling normally, no stimulus detected.
        Suspicious = 1,  // Partial stimulus — slows down, moves toward POI.
        Alert      = 2,  // Confirmed something — walks toward POI, propagates alarm.
        Combat     = 3,  // Direct visual — full engagement, never decays while HasVisual.
    }

    #endregion

    public enum MusicArea
    {
        Menu = 0,
        OnBoarding = 1,
        Base = 2,
        Zone_1 = 3,
        Viewer = 4,
        
    }

    #region SkillTree

    public enum UpgradeType
    {
        Health,
        Defense,
        Stamina,
        Speed,
        Attack,
        Weight,
        Inventory
    }

    public enum ProgressionType
    {
        Manual,
        Linear,
        Multiplicative,
        Power,
        Exponential,
        Logarithmic,
        DiminishingReturns
    }

    #endregion
}
