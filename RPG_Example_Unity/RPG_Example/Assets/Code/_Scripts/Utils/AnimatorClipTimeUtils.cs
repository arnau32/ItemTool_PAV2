using System.Collections.Generic;
using UnityEngine;

public static class AnimatorClipTimeUtils
{
    private static readonly List<AnimatorClipInfo> SharedClipBuffer = new List<AnimatorClipInfo>();

    public static bool TryGetClipNormalizedTime(Animator animator, int layer, AnimationClip targetClip,
        out float tNorm, out float rawNorm, out bool foundInOutgoingState)
    {
        tNorm = rawNorm = 0f;
        foundInOutgoingState = false;

        if (!animator || !targetClip) return false;

        bool inTransition = animator.IsInTransition(layer);

        var stateInfo = inTransition
            ? animator.GetNextAnimatorStateInfo(layer)
            : animator.GetCurrentAnimatorStateInfo(layer);

        if (inTransition)
            animator.GetNextAnimatorClipInfo(layer, SharedClipBuffer);
        else
            animator.GetCurrentAnimatorClipInfo(layer, SharedClipBuffer);

        int count = SharedClipBuffer.Count;

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                if (SharedClipBuffer[i].clip != targetClip) continue;

                rawNorm = stateInfo.normalizedTime;
                tNorm = rawNorm - Mathf.Floor(rawNorm);
                return true;
            }
        }

        if (inTransition)
        {
            animator.GetCurrentAnimatorClipInfo(layer, SharedClipBuffer);
            count = SharedClipBuffer.Count;

            var currentStateInfo = animator.GetCurrentAnimatorStateInfo(layer);

            for (int i = 0; i < count; i++)
            {
                if (SharedClipBuffer[i].clip != targetClip) continue;

                rawNorm = currentStateInfo.normalizedTime;
                tNorm = rawNorm - Mathf.Floor(rawNorm);
                foundInOutgoingState = true;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetClipNormalizedTime(Animator animator, int layer, AnimationClip targetClip, out float tNorm, out float rawNorm)
    {
        return TryGetClipNormalizedTime(animator, layer, targetClip, out tNorm, out rawNorm, out _);
    }
}