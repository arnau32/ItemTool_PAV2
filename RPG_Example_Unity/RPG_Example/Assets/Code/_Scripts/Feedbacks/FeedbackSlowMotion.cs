using System.Collections;
using UnityEngine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("SpecialEffects")]
    public class FeedbackSlowMotion : FeedbackBase
    {
        // 0 = full stop, 1 = normal speed. Values above 1 are clamped to 1 by
        // TimeScaleManager, which means no visible effect — the slider prevents that mistake.
        [Range(0f, 1f)] public float slowFactor = 0.3f;
        public float slowDuration;

        private Coroutine _activeCoroutine;
        private const string SLOW_ID = "FeedbackSlowMotion";

        public override void Play(GameObject owner)
        {
            if (!active) return;

            var runner = GameServices.Get<CoroutineRunner>();
            var timeManager = GameServices.Get<TimeScaleManager>();

            if (_activeCoroutine != null)
            {
                runner.StopCoroutine(_activeCoroutine);
                timeManager.ReleaseSlow(SLOW_ID);
            }

            _activeCoroutine = runner.StartCoroutine(SlowMotionCoroutine(timeManager));
        }

        private IEnumerator SlowMotionCoroutine(TimeScaleManager timeManager)
        {
            timeManager.RequestSlow(SLOW_ID, slowFactor);
            yield return new WaitForSecondsRealtime(slowDuration);
            timeManager.ReleaseSlow(SLOW_ID);
            _activeCoroutine = null;
        }
    }
}