using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    // -- Para heavy hits, parry, perfect dodge (muy corto).
    public class FeedbackCameraDutchKickCinemachine : FeedbackBase
    {
        [Tooltip("If null, will try to find the active CinemachineCamera in the scene.")] public CinemachineCamera cmCamera;

        [Header("Dutch Kick (degrees)")]
        public float kickAmount = 3f;
        public bool randomSign = true;

        [Header("Timing")]
        public float inDuration = 0.06f;
        public float holdDuration = 0.00f;
        public float outDuration = 0.10f;

        public bool useUnscaledTime = true;

        private Coroutine _activeCoroutine;
        private float _baselineDutch;
        private bool _hasBaseline;

        public override void Play(GameObject owner)
        {
            if (!active) return;

            if (cmCamera == null)
                cmCamera = Object.FindFirstObjectByType<CinemachineCamera>();

            if (cmCamera == null) return;

            var runner = GameServices.Get<CoroutineRunner>();
            if (runner == null) return;

            if (_activeCoroutine != null)
            {
                runner.StopCoroutine(_activeCoroutine);
                RestoreBaseline();
            }

            CacheBaseline();
            _activeCoroutine = runner.StartCoroutine(KickCoroutine());
        }

        private void CacheBaseline()
        {
            _baselineDutch = cmCamera.Lens.Dutch;
            _hasBaseline = true;
        }

        private void RestoreBaseline()
        {
            if (!_hasBaseline || cmCamera == null) return;

            var lens = cmCamera.Lens;
            lens.Dutch = _baselineDutch;
            cmCamera.Lens = lens;
        }

        private IEnumerator KickCoroutine()
        {
            float start = _baselineDutch;

            float sign = 1f;
            if (randomSign) sign = Random.value < 0.5f ? -1f : 1f;

            float target = start + kickAmount * sign;

            yield return LerpDutch(start, target, Mathf.Max(0.0001f, inDuration));

            if (holdDuration > 0f)
                yield return Wait(holdDuration);

            yield return LerpDutch(target, start, Mathf.Max(0.0001f, outDuration));

            _activeCoroutine = null;
        }

        private IEnumerator LerpDutch(float a, float b, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += DeltaTime() / duration;
                float v = Mathf.Lerp(a, b, Mathf.Clamp01(t));

                var lens = cmCamera.Lens;
                lens.Dutch = v;
                cmCamera.Lens = lens;

                yield return null;
            }
        }

        private IEnumerator Wait(float seconds)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(seconds);
            else yield return new WaitForSeconds(seconds);
        }

        private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
