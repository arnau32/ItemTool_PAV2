using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BaseParry", menuName = "Combat/Parry", order = 0)]
public class ParryData : ScriptableObject, ICombatMotionData
{
    #region Animation

    [Header("Animation")]
    public string triggerAnimation;
    public AnimationClip animationClip;
    public float crossFade;
    public float staminaCost;

    #endregion

    #region Tunning 

    [Header("Tuning")]
    [Tooltip("Small input buffer. If parry is pressed during cooldown, it can auto-fire when allowed.")] public float inputBufferSeconds = 0.12f;
    [Tooltip("Minimum time before another parry attempt is allowed (spam control).")] public float cooldownSeconds = 0.10f;
    [Tooltip("Extra lockout applied when the parry attempt FAILS (no deflect).")] public float failLockoutSeconds = 0.25f;
    [Tooltip("Extra lockout applied when the parry attempt SUCCEEDS (deflect).")] public float successLockoutSeconds = 0.08f;

    #endregion

    #region Animation Playing

    [NonSerialized] private bool _hashInit;
    [NonSerialized] private int _triggerHash;

    public int TriggerAnimationHash()
    {
        if (_hashInit)
        {
            return _triggerHash;
        }
        _hashInit = true;
        _triggerHash = string.IsNullOrEmpty(triggerAnimation) ? 0 : Animator.StringToHash(triggerAnimation);

        return _triggerHash;
    }

    #endregion

    [Header("Animation Speed Curve")]
    [Tooltip("If enabled, animator speed multiplier will be driven by this curve using normalized time (0..1).")] public bool useAnimatorSpeedCurve = false;
    public AnimationCurve animatorSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public float EvaluateAnimatorSpeed(float normalizedTime01)
    {
        if (!useAnimatorSpeedCurve) return 1f;

        var t = Mathf.Clamp01(normalizedTime01);

        if (animatorSpeedCurve == null || animatorSpeedCurve.length == 0) return 1f;

        var speed = animatorSpeedCurve.Evaluate(t);

        return Mathf.Max(0.01f, speed);
    }

    [Header("Root Motion Multiplier Curve")]
    [Tooltip("If enabled, root motion will be scaled by this curve using normalized time (0..1)")] public bool useRootMotionMultiplier = false;
    public AnimationCurve rootMotionMultiplierCurve = AnimationCurve.Constant(0f, 1f, 1f);

    public float EvaluateRootMotionMultiplier(float normalizedTime01)
    {
        if (!useRootMotionMultiplier) return 1f;

        var t = Mathf.Clamp01(normalizedTime01);

        if (rootMotionMultiplierCurve == null || rootMotionMultiplierCurve.length == 0) return 1f;

        return Mathf.Max(0f, rootMotionMultiplierCurve.Evaluate(t));
    }

    [Header("Procedural Forward Move")]
    [Tooltip("If enabled, adds a forward step driven by a curve (progress 0..1 over normalized time). Useful for clips without root motion.")] public bool useProceduralForwardMove = false;
    public AnimationCurve proceduralForwardMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float proceduralForwardMoveDistance = 0.25f;

    public float EvaluateProceduralForwardProgress01(float normalizedTime01)
    {
        if (!useProceduralForwardMove) return 0f;

        var t = Mathf.Clamp01(normalizedTime01);

        if (proceduralForwardMoveCurve == null || proceduralForwardMoveCurve.length == 0)
            return 0f;

        return Mathf.Clamp01(proceduralForwardMoveCurve.Evaluate(t));
    }

    #region Poise Damage
 
    [Header("Poise Damage")]
    [Tooltip("Poise damage dealt to the attacker on a normal parry.")]
    public float poiseDamage = 30f;
 
    [Tooltip("Poise damage dealt to the attacker on a perfect parry.")]
    public float perfectPoiseDamage = 55f;
 
    #endregion

    #region Parry Windows

    [WindowType("Normal Parry", 0.95f, 0.85f, 0.25f, trackOrder: 1, StructureType = WindowStructureType.Single)]
    public WindowEvent parryWindow;

    [WindowType("Perfect Parry", 0.95f, 0.85f, 0.25f, trackOrder: 2, StructureType = WindowStructureType.Single)]
    public WindowEvent perfectParryWindow;

    public (bool, Enums.ParryQuality) IsParryActive(float normalizedTime)
    {
        if (perfectParryWindow.IsActive(normalizedTime)) return (true, Enums.ParryQuality.Perfect);

        return parryWindow.IsActive(normalizedTime) ? (true, Enums.ParryQuality.Normal) : (false, Enums.ParryQuality.None);
    }

    #endregion

    #region Windows

    [WindowType("VFX", 0.15f, 0.9f, 0.55f, trackOrder: 3, StructureType = WindowStructureType.SpecialVfx)]
    public List<AttackVfxWindow> vfxWindows = new List<AttackVfxWindow>();

    public bool allowDodgeCancelWindow;
    [WindowType("On Successful: Dodge Cancel", 0.2f, 0.6f, 1f, trackOrder: 4, EnableFieldName = "allowDodgeCancelWindow", StructureType = WindowStructureType.Single)]
    public WindowEvent dodgeCancelWindow;

    public bool allowMoveCancel;
    [WindowType("On Successful: Move Cancel", 0.2f, 0.6f, 1f, trackOrder: 5, EnableFieldName = "allowMoveCancel", StructureType = WindowStructureType.Single)]
    public WindowEvent moveCancelWindow;
    
    [WindowType("Dodge Cancel", 0.2f, 0.6f, 1f, trackOrder: 6, StructureType = WindowStructureType.Single)]
    public WindowEvent permaDodgeCancelWindow;
    
    [WindowType("Move Cancel", 0.2f, 0.6f, 1f, trackOrder: 7, EnableFieldName = "allowAttackCancel", StructureType = WindowStructureType.Single)]
    public WindowEvent permaMoveCancelWindow;

    public bool allowAttackCancel;
    [WindowType("On Successful: Attack Cancel", 0.2f, 0.6f, 1f, trackOrder: 8, EnableFieldName = "allowAttackCancel", StructureType = WindowStructureType.Single)]
    public WindowEvent attackCancelWindows;

    public bool allowMoveRotation;
    [WindowType("Move Rotation", 0.6f, 0.4f, 1f, trackOrder: 9, EnableFieldName = "allowMoveRotation", StructureType = WindowStructureType.List)]
    public List<WindowEvent> moveRotationWindows;

    #endregion

    #region ICombatMotionData Implementation

    public bool UseRootMotionMultiplier => useRootMotionMultiplier;
    public bool UseProceduralForwardMove => useProceduralForwardMove;
    public float ProceduralForwardMoveDistance => proceduralForwardMoveDistance;

    #endregion

}
