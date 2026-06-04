
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;


namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    public class FeedbackCameraPositionComposerKickCinemachine : FeedbackBase
    {
        [Tooltip("If null, will try to find one in the scene.")] public CinemachinePositionComposer composer;

        [Header("Camera Distance Kick")]
        public bool kickDistance = true;
        public float distanceDelta = 2f;

        [Header("Target Offset Kick")]
        public bool kickTargetOffset = false;
        public Vector3 targetOffsetDelta = new Vector3(0f, 0.25f, 0f);

        [Header("Damping Override")]
        public bool overrideDamping = false;
        public Vector3 dampingOverride = new Vector3(0.15f, 0.20f, 0.25f);

        [Header("Timing")]
        public float inDuration = 0.12f;
        public float holdDuration = 0.08f;
        public float outDuration = 0.20f;
        public bool useUnscaledTime = true;

        private Coroutine _activeCoroutine;

        private float _baseDistance;
        private Vector3 _baseTargetOffset;
        private Vector3 _baseDamping;
        private bool _hasBaseline;

        public override void Play(GameObject owner)
        {
            if (!active) return;

            if (composer == null)
                composer = Object.FindFirstObjectByType<CinemachinePositionComposer>();

            if (composer == null) return;

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
            _baseDistance = composer.CameraDistance;
            _baseTargetOffset = composer.TargetOffset;
            _baseDamping = composer.Damping;
            _hasBaseline = true;
        }

        private void RestoreBaseline()
        {
            if (!_hasBaseline || composer == null) return;

            composer.CameraDistance = _baseDistance;
            composer.TargetOffset = _baseTargetOffset;
            composer.Damping = _baseDamping;
        }

        private IEnumerator KickCoroutine()
        {
            float startDistance = _baseDistance;
            float targetDistance = kickDistance ? startDistance + distanceDelta : startDistance;

            Vector3 startOffset = _baseTargetOffset;
            Vector3 targetOffset = kickTargetOffset ? startOffset + targetOffsetDelta : startOffset;

            Vector3 startDamping = _baseDamping;
            Vector3 targetDamping = overrideDamping ? dampingOverride : startDamping;

            yield return LerpComposer(
                startDistance, targetDistance,
                startOffset, targetOffset,
                startDamping, targetDamping,
                Mathf.Max(0.0001f, inDuration)
            );

            if (holdDuration > 0f)
                yield return Wait(holdDuration);

            yield return LerpComposer(
                targetDistance, startDistance,
                targetOffset, startOffset,
                targetDamping, startDamping,
                Mathf.Max(0.0001f, outDuration)
            );

            _activeCoroutine = null;
        }

        private IEnumerator LerpComposer(
            float distA, float distB,
            Vector3 offA, Vector3 offB,
            Vector3 dampA, Vector3 dampB,
            float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += DeltaTime() / duration;
                float k = Mathf.Clamp01(t);

                composer.CameraDistance = Mathf.Lerp(distA, distB, k);
                composer.TargetOffset = Vector3.Lerp(offA, offB, k);
                composer.Damping = Vector3.Lerp(dampA, dampB, k);

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
