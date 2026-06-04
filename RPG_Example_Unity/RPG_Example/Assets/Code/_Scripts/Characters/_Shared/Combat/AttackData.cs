using System;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BaseAttack", menuName = "Combat/Attack")]
public class AttackData : ScriptableObject, ICombatMotionData
{
    public string triggerAnimation;
    public AnimationClip animationClip;
    [Range(0, 1)] public float timeToChangeAnim;
    public float crossFade;

    public float AnimationLength => animationClip.length;
    public float TimeToNextCombo => AnimationLength * timeToChangeAnim;

    [NonSerialized] private bool _hashInit;
    [NonSerialized] private int _triggerHash;

    [Header("Ability Score")] public float abilityScoreOnHit = 20f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure the cached hash is rebuilt if the string changes in the editor.
        _hashInit = false;
        _triggerHash = 0;
    }
#endif

    public int TriggerAnimationHash()
    {
        if (_hashInit) return _triggerHash;

        _hashInit = true;
        _triggerHash = string.IsNullOrEmpty(triggerAnimation) ? 0 : Animator.StringToHash(triggerAnimation);
        return _triggerHash;
    }

    [Header("Animation Speed Curve")] [Tooltip("If enabled, animator speed multiplier will be driven by this curve using normalized time (0..1).")]
    public bool useAnimatorSpeedCurve = false;

    public AnimationCurve animatorSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public float EvaluateAnimatorSpeed(float normalizedTime01)
    {
        if (!useAnimatorSpeedCurve) return 1f;
        if (animatorSpeedCurve == null || animatorSpeedCurve.length == 0) return 1f;

        var t = Mathf.Clamp01(normalizedTime01);
        var speed = animatorSpeedCurve.Evaluate(t);
        return Mathf.Max(0.01f, speed);
    }

    [Header("Root Motion Multiplier Curve")] [Tooltip("If enabled, root motion will be scaled by this curve using normalized time (0..1)")]
    public bool useRootMotionMultiplier = false;

    public AnimationCurve rootMotionMultiplierCurve = AnimationCurve.Constant(0f, 1f, 1f);

    public float EvaluateRootMotionMultiplier(float normalizedTime01)
    {
        if (!useRootMotionMultiplier) return 1f;
        if (rootMotionMultiplierCurve == null || rootMotionMultiplierCurve.length == 0) return 1f;

        var t = Mathf.Clamp01(normalizedTime01);
        return Mathf.Max(0f, rootMotionMultiplierCurve.Evaluate(t));
    }

    [Header("Procedural Forward Move")] [Tooltip("If enabled, adds a forward step driven by a curve (progress 0..1 over normalized time). Useful for clips without root motion.")]
    public bool useProceduralForwardMove = false;

    public AnimationCurve proceduralForwardMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float proceduralForwardMoveDistance = 0.6f;

    public float EvaluateProceduralForwardProgress01(float normalizedTime01)
    {
        if (!useProceduralForwardMove) return 0f;
        if (proceduralForwardMoveCurve == null || proceduralForwardMoveCurve.length == 0) return 0f;

        var t = Mathf.Clamp01(normalizedTime01);
        return Mathf.Clamp01(proceduralForwardMoveCurve.Evaluate(t));
    }

    [Header("Attack Information")] public string attackName;
    public Enums.HitType attackHitType;
    public float staminaCost;
    public float damage;
    [Tooltip("Multiplies the attacker's Attack stat for this hit. 1 = normal, 1.5 = heavy, 0.6 = light.")]
    [Range(0.1f, 5f)] public float damageMultiplier = 1f;

    [Header("Poise")] [Tooltip("Poise damage dealt to the enemy on hit. Light attacks: ~10, Heavy: ~35, Skill: ~60.")]
    public float poiseDamage = 15f;

    [Header("Hyper Armor")] [Tooltip("If true, the player cannot be interrupted by Knockback hits during this attack. Knockdown always interrupts.")]
    public bool hasHyperArmor = false;

    #region Window Events

    [Header("Window Events")] [WindowType("Damage", 0.18f, 0.75f, 0.25f, trackOrder: 0, StructureType = WindowStructureType.SpecialDamage)]
    public List<DamageWindow> damageWindows = new List<DamageWindow>();

    [WindowType("VFX", 0.15f, 0.9f, 0.55f, trackOrder: 1, StructureType = WindowStructureType.SpecialVfx)]
    public List<AttackVfxWindow> vfxWindows = new List<AttackVfxWindow>();

    [WindowType("Weapon VFX", 0.15f, 0.9f, 0.55f, trackOrder: 2, StructureType = WindowStructureType.SpecialVfx)]
    public List<WeaponVfxWindow> weaponVfxWindows = new List<WeaponVfxWindow>();

    [WindowType("Audio Trigger", 0.9f, 0.55f, 0.15f, trackOrder: 3, StructureType = WindowStructureType.SpecialAudioTrigger)]
    public List<AudioTrigger> audioTriggers = new List<AudioTrigger>();

    [WindowType("Audio Window", 0.9f, 0.55f, 0.15f, trackOrder: 4, StructureType = WindowStructureType.SpecialAudioWindow)]
    public List<AudioWindow> audioWindows = new List<AudioWindow>();

    public bool usesCombo;

    [WindowType("Combo", 1f, 0.65f, 0.15f, trackOrder: 5, EnableFieldName = "usesCombo", StructureType = WindowStructureType.Single)]
    public WindowEvent comboWindow;

    public bool allowDodgeCancelWindow;

    [WindowType("Dodge Cancel", 0.2f, 0.6f, 1f, trackOrder: 6, EnableFieldName = "allowDodgeCancelWindow", StructureType = WindowStructureType.List)]
    public List<WindowEvent> dodgeCancelWindows;

    public bool allowMoveCancel;

    [WindowType("Move Cancel", 0.2f, 0.6f, 1f, trackOrder: 7, EnableFieldName = "allowMoveCancel", StructureType = WindowStructureType.List)]
    public List<WindowEvent> moveCancelWindows;

    public bool allowMoveRotation;

    [WindowType("Move Rotation", 0.6f, 0.4f, 1f, trackOrder: 8, EnableFieldName = "allowMoveRotation", StructureType = WindowStructureType.List)]
    public List<WindowEvent> moveRotationWindows;

    public bool allowCanBeParriedWindow;

    [WindowType("Can Be Parried", 0.95f, 0.85f, 0.25f, trackOrder: 9, EnableFieldName = "allowCanBeParriedWindow", StructureType = WindowStructureType.List)]
    public List<WindowEvent> canBeParriedWindows = new List<WindowEvent>();

    [WindowType("Parry Cancel", 0.95f, 0.85f, 0.25f, trackOrder: 10, StructureType = WindowStructureType.List)]
    public List<WindowEvent> parryCancel;

    public bool useHardLookWindow = false;

    [WindowType("Hard Look", 0.35f, 0.75f, 1f, trackOrder: 11, EnableFieldName = "useHardLookWindow", StructureType = WindowStructureType.List)]
    public List<WindowEvent> hardLookWindow;

    public bool IsHardLookWindowActive(float tNorm)
    {
        if (!useHardLookWindow) return false;

        for (int i = 0; i < hardLookWindow.Count; i++)
        {
            if (hardLookWindow[i].IsActive(tNorm))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    public virtual bool Execute(Action<string, float> playAnimation)
    {
        playAnimation?.Invoke(triggerAnimation, crossFade);
        return true;
    }

    #region ICombatMotionData Implementation

    public bool UseRootMotionMultiplier => useRootMotionMultiplier;
    public bool UseProceduralForwardMove => useProceduralForwardMove;
    public float ProceduralForwardMoveDistance => proceduralForwardMoveDistance;

    #endregion
}