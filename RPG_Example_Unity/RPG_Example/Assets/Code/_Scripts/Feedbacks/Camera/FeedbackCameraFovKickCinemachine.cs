using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    public class FeedbackCameraFovKickCinemachine : FeedbackBase
    {
        [Tooltip("If null, will try to find the active CinemachineCamera in the scene.")] public CinemachineCamera cmCamera;

        [Header("FOV Kick (degrees)")]
        public float kickAmount = 3f;

        [Header("Timing")]
        public float inDuration = 0.08f;
        public float holdDuration = 0.00f;
        public float outDuration = 0.14f;

        [Tooltip("If true, uses unscaled time.")]
        public bool useUnscaledTime = true;

        private Coroutine _activeCoroutine;
        private float _baselineFov;
        private bool _hasBaseline;

        public override void Play(GameObject owner)
        {
            if (!active) return;

            if (cmCamera == null)
            {
                cmCamera = Object.FindFirstObjectByType<CinemachineCamera>();
            }

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
            _baselineFov = cmCamera.Lens.FieldOfView;
            _hasBaseline = true;
        }

        private void RestoreBaseline()
        {
            if (!_hasBaseline || cmCamera == null) return;

            var lens = cmCamera.Lens;
            lens.FieldOfView = _baselineFov;
            cmCamera.Lens = lens;
        }

        private IEnumerator KickCoroutine()
        {
            float start = _baselineFov;
            float target = start + kickAmount;

            yield return LerpFov(start, target, Mathf.Max(0.0001f, inDuration));

            if (holdDuration > 0f)
                yield return Wait(holdDuration);

            yield return LerpFov(target, start, Mathf.Max(0.0001f, outDuration));

            _activeCoroutine = null;
        }

        private IEnumerator LerpFov(float a, float b, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += DeltaTime() / duration;
                float v = Mathf.Lerp(a, b, Mathf.Clamp01(t));

                var lens = cmCamera.Lens;
                lens.FieldOfView = v;
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
