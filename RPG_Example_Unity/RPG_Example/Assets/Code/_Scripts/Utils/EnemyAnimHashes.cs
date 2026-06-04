using UnityEngine;

public static class EnemyAnimHashes
{
    public const int LayerBase = 0;
    public const int LayerOverride = 1;
    public const int LayerUpwards = 2;

    public static readonly int HashDirX = Animator.StringToHash("dirX");

    public static readonly int HashDirY = Animator.StringToHash("dirY");
    public static readonly int HashSpeed = Animator.StringToHash("speed");
    public static readonly int HashIsWandering = Animator.StringToHash("isWandering");

    public static readonly int HashSpawn = Animator.StringToHash("Spawn");
    public static readonly int HashDead = Animator.StringToHash("Dead");
    public static readonly int HashKnockdown = Animator.StringToHash("Knockdown");
    public static readonly int HashKnockback = Animator.StringToHash("Knockback");
    public static readonly int HashKnockbackParry = Animator.StringToHash("KnockbackParry");
    public static readonly int HashStun = Animator.StringToHash("Stun");

    public static int HashHit(int n) => Animator.StringToHash("hit_" + n);

    public static readonly int HashDodge = Animator.StringToHash("Dodge");
    public static readonly int HashHeal  = Animator.StringToHash("Heal");

    public static readonly int HashTurnRight    = Animator.StringToHash("RightTurn");
    public static readonly int HashTurnLeft     = Animator.StringToHash("LeftTurn");
    public static readonly int HashStopRotation = Animator.StringToHash("StopRotation");
}

public static class SharedHashes
{
    public static readonly int HashKnockdownDone = Animator.StringToHash("KD_Done");
    
}

public static class PlayerAnimHashes
{
    public const int LayerBase = 0;
    public const int LayerOverride = 1;
    public const int LayerUpwards = 2;

    public static readonly int AttackTag = Animator.StringToHash("Attack");
    public static readonly int HashDodgeSpeedParameter = Animator.StringToHash("DodgeSpeed");
    public static readonly int HashAttackSpeedParameter = Animator.StringToHash("AtkSpeed");
    public static readonly int HashDirX = Animator.StringToHash("dirX");
    public static readonly int HashDirY = Animator.StringToHash("dirY");
    public static readonly int HashSpeed = Animator.StringToHash("speed");
    public static readonly int IsLockedBool = Animator.StringToHash("IsLocked");
    public static readonly int CancelMovementWindow = Animator.StringToHash("CancelMovement");

    // Playable actions
    public static readonly int Consume = Animator.StringToHash("Consume");
    public static readonly int ChestOpen = Animator.StringToHash("Player_ChestOpen");

    public static readonly int IsInDialogue = Animator.StringToHash("IsInDialogue");
}