﻿using UnityEngine;

public sealed class EnemyCombatRootMotionMotor
{
    private readonly EnemyContext _ctx;
    private readonly float _extraStopDistance;

    // Procedural move state
    private ICombatMotionData _lastMotionData;
    private float _prevProc01;

    public EnemyCombatRootMotionMotor(EnemyContext ctx, float extraStopDistance)
    {
        _ctx = ctx;
        _extraStopDistance = Mathf.Max(0f, extraStopDistance);

        _lastMotionData = null;
        _prevProc01 = 0f;
    }

    public void ApplyAttackRootMotion()
    {
        if (_ctx == null || _ctx.Animation == null || _ctx.CombatPlanner == null)
        {
            ResetProceduralState();
            return;
        }

        if (!_ctx.CombatPlanner.IsAttackRuntimeActive)
        {
            ResetProceduralState();
            return;
        }

        ICombatMotionData motionData = _ctx.CombatPlanner.ActiveAttack;
        float normalizedTime = _ctx.CombatPlanner.AttackNormalizedTime;

        if (motionData == null)
        {
            ResetProceduralState();
            return;
        }

        // Reset if motion has been changed
        if (motionData != _lastMotionData)
        {
            _lastMotionData = motionData;
            _prevProc01 = 0f;
        }

        var animator = _ctx.Animation.Animator;

        var delta = animator.deltaPosition;
        Quaternion nextRot = _ctx.Owner.transform.rotation * animator.deltaRotation;

        ApplyRootMotionMultiplier(motionData, normalizedTime, ref delta);
        ApplyProceduralForwardMove(motionData, normalizedTime, ref delta);
        
        ApplyCombatRootmotionClampOvershootSafe(ref delta);

        ApplyMovement(delta, nextRot);
    }

    private void ResetProceduralState()
    {
        _lastMotionData = null;
        _prevProc01 = 0f;
    }

    private static void ApplyRootMotionMultiplier(ICombatMotionData motionData, float normalizedTime, ref Vector3 delta)
    {
        if (!motionData.UseRootMotionMultiplier) return;

        var multiplier = motionData.EvaluateRootMotionMultiplier(normalizedTime);
        delta.x *= multiplier;
        delta.z *= multiplier;
    }

    private void ApplyProceduralForwardMove(ICombatMotionData motionData, float normalizedTime, ref Vector3 delta)
    {
        if (!motionData.UseProceduralForwardMove) return;
        if (motionData.ProceduralForwardMoveDistance <= 0f) return;

        float proc01 = motionData.EvaluateProceduralForwardProgress01(normalizedTime);
        float d01 = Mathf.Max(0f, proc01 - _prevProc01);
        _prevProc01 = proc01;
        Vector3 dir;
        
        if (_ctx.Movement.target != null)
        {
            bool hardLook = _ctx.CombatPlanner.CurrentAction.useHardLook ||
                (_ctx.CombatPlanner.ActiveAttack?.IsHardLookWindowActive(normalizedTime) ?? false);
            dir = hardLook ? _ctx.Movement.DirToTarget() : GetProceduralForwardDirection();
        }
        else
        {
            dir = GetProceduralForwardDirection();
        }


        Vector3 add = dir * (d01 * motionData.ProceduralForwardMoveDistance);
        add.y = 0f;

        delta += add;
    }

    private Vector3 GetProceduralForwardDirection()
    {
        var movement = _ctx.Movement;
        if (movement != null)
        {
            Vector3 forced = UtilsNagu.Flatten(movement.ForcedAttackDirection);
            if (UtilsNagu.HasDirection(forced))
            {
                forced.Normalize();
                return forced;
            }
        }

        Vector3 forward = _ctx.Owner.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < UtilsNagu.EPSILON_DIR_SQR)
            return Vector3.forward;

        forward.Normalize();
        return forward;
    }
    
    // Prevents crossing inside the stop distance even if delta is large (single-frame overshoot).
    private void ApplyCombatRootmotionClampOvershootSafe(ref Vector3 delta)
    {
        var movement = _ctx.Movement;
        if (movement == null || movement.target == null) return;

        Transform target = movement.target;

        Vector3 from = _ctx.Owner.transform.position;
        Vector3 to = target.position;

        from.y = 0f;
        to.y = 0f;

        Vector3 toTarget = to - from;
        float dist = toTarget.magnitude;

        if (dist < UtilsNagu.EPSILON_DIR) return;

        Vector3 dirToTarget = toTarget / dist;
        dirToTarget.y = 0f;

        float forwardComponent = Vector3.Dot(delta, dirToTarget);

        // If not moving towards the target, nothing to clamp.
        if (forwardComponent <= 0f) return;

        float stopDist = GetEffectiveStopDistance(target);

        // Ensure: newDist = dist - forwardComponent >= stopDist -> forwardComponent <= dist - stopDist
        float maxForward = dist - stopDist;

        if (maxForward <= 0f)
        {
            // Already inside stop distance: remove all forward push.
            delta -= dirToTarget * forwardComponent;
            return;
        }

        if (forwardComponent > maxForward)
        {
            delta -= dirToTarget * (forwardComponent - maxForward);
        }
    }
    
    private float GetEffectiveStopDistance(Transform target)
    {
        float stop = _extraStopDistance;

        if (_ctx.Agent != null) stop += _ctx.Agent.radius;

        float targetRadius = 0f;

        if (target.TryGetComponent<CharacterController>(out var cc))
        {
            targetRadius = cc.radius;
        }
        else if (target.TryGetComponent<CapsuleCollider>(out var cap))
        {
            float maxScale = Mathf.Max(target.lossyScale.x, target.lossyScale.z);
            targetRadius = cap.radius * maxScale;
        }
        else if (target.TryGetComponent<SphereCollider>(out var sph))
        {
            float maxScale = Mathf.Max(target.lossyScale.x, target.lossyScale.z);
            targetRadius = sph.radius * maxScale;
        }

        stop += targetRadius;

        // Small safety margin against floating point / animation sampling.
        stop += 0.03f;

        return Mathf.Max(0.01f, stop);
    }

    private void ApplyMovement(Vector3 delta, Quaternion nextRot)
    {
        delta.y = 0f;

        var tr = _ctx.Owner.transform;
        tr.position += delta;
        tr.rotation = nextRot;

        if (_ctx.Agent != null && _ctx.Agent.enabled)
        {
            _ctx.Agent.nextPosition = tr.position;
        }
    }
}
