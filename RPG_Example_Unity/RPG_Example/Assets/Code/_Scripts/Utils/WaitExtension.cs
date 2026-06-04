using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public static class WaitExtension
{
    public static void Wait(float delay, UnityAction action)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(ExecuteAction(delay, action));
    }

    public static void WaitFrame(UnityAction action)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(ExecuteNextFrame(action));
    }

    public static void WaitFrames(int frameCount, UnityAction action)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(ExecuteFrames(frameCount, action));
    }

    public static void WaitForAnimation(Animator animator, UnityAction onComplete)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(WaitForAnimationRoutineWithDelay(animator, 0f, onComplete));
    }

    public static void WaitForAnimationOnLayer(Animator animator, int layer, UnityAction onComplete)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(WaitForAnimationRoutineOnLayer(animator, layer, onComplete));
    }

    public static void WaitForAnimationOnLayerWithDelay(Animator animator, int layer, float delay, UnityAction onComplete)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(WaitForAnimationRoutineOnLayerWithDelay(animator, layer, delay, onComplete));
    }

    public static void WaitForAnimationWithDelay(Animator animator, float delay, UnityAction onComplete)
    {
        GameServices.Get<CoroutineRunner>().StartCoroutine(WaitForAnimationRoutineWithDelay(animator, delay, onComplete));
    }

    private static IEnumerator WaitForAnimationRoutineWithDelay(Animator animator, float delay, UnityAction onComplete)
    {
        yield return null;

        var info = animator.GetCurrentAnimatorClipInfo(0);
        float length = info.Length > 0 ? info[0].clip.length : 0f;
        length += delay;
        yield return new WaitForSeconds(length);
        onComplete?.Invoke();
    }

    private static IEnumerator WaitForAnimationRoutineOnLayer(Animator animator, int layer, UnityAction onComplete)
    {
        yield return null;

        AnimatorClipInfo[] info = animator.IsInTransition(layer)
            ? animator.GetNextAnimatorClipInfo(layer)
            : animator.GetCurrentAnimatorClipInfo(layer);

        float length = info.Length > 0 ? info[0].clip.length : 0f;
        yield return new WaitForSeconds(length);
        onComplete?.Invoke();
    }

    private static IEnumerator WaitForAnimationRoutineOnLayerWithDelay(Animator animator, int layer, float delay, UnityAction onComplete)
    {
        yield return null;

        AnimatorClipInfo[] info = animator.IsInTransition(layer)
            ? animator.GetNextAnimatorClipInfo(layer)
            : animator.GetCurrentAnimatorClipInfo(layer);

        float length = info.Length > 0 ? info[0].clip.length : 0f;
        length += delay;
        yield return new WaitForSeconds(length);
        onComplete?.Invoke();
    }

    private static IEnumerator ExecuteFrames(int frameCount, UnityAction action)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }

        action?.Invoke();
    }

    private static IEnumerator ExecuteNextFrame(UnityAction action)
    {
        yield return null; // 1 frame
        action?.Invoke();
    }

    private static IEnumerator ExecuteAction(float delay, UnityAction action)
    {
        yield return new WaitForSecondsRealtime(delay);
        action?.Invoke();
    }
}