using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Dodge", menuName = "Combat/Player Dodge", order = 0)]
public class DodgeData : ScriptableObject
{
    [Header("Information")]
    public string animationName;
    public float crossFade = 0.15f;
    
    public AnimationClip animationClip;
    public float AnimationLength => animationClip.length;
    
    [Header("Dodge Movement Speed")] 
    public AnimationCurve dodgeSpeedCurve = AnimationCurve.Linear(0, 5, 1, 2);

    public float EvaluateDodgeSpeed(float normalizedTime)
    {
        if (dodgeSpeedCurve == null || dodgeSpeedCurve.length == 0) return 5f; // Default fallback
    
        return Mathf.Max(0.01f, dodgeSpeedCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
    }
    
    [Header("Animation Speed Curve")]
    [Tooltip("If enabled, animator DodgeSpeed multiplier will be driven by this curve using normalized time (0..1).")] public bool useAnimatorSpeedCurve = false;

    [Tooltip("Curve output is final DodgeSpeed multiplier (e.g. 0.8..2.0) evaluated over normalized time (0..1).")]
    public AnimationCurve animatorSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public float EvaluateAnimatorSpeed(float normalizedTime01)
    {
        if (!useAnimatorSpeedCurve) return 1f;

        var t = Mathf.Clamp01(normalizedTime01);

        if (animatorSpeedCurve == null || animatorSpeedCurve.length == 0) return 1f;

        var speed = animatorSpeedCurve.Evaluate(t);

        return Mathf.Max(0.01f, speed);
    }
    
    [Header("Animation Event")] 
    [WindowType("iFrame", 0.10f, 0.85f, 0.25f, trackOrder: 0, StructureType = WindowStructureType.Single)]
    public WindowEvent iFrameWindow;

    [WindowType("Buffer End", 0.95f, 0.75f, 0.10f, trackOrder: 1, StructureType = WindowStructureType.Single)]
    public WindowEvent onBufferEndDodgeAnimation;

    [WindowType("Movement Cancel", 0.10f, 0.80f, 0.90f, trackOrder: 2, StructureType = WindowStructureType.Single)]
    public WindowEvent movementCancelation;

    [WindowType("Attack Buffer", 0.85f, 0.20f, 0.90f, trackOrder: 3, StructureType = WindowStructureType.Single)]
    public WindowEvent attackBufferWindow;

    public void Execute(Action<string, float> playTargetAnimation)
    {
        playTargetAnimation?.Invoke(animationName, crossFade);
    }
}
