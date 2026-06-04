
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;


namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    public class FeedbackCameraPositionComposerStateCinemachine : FeedbackBase
    {
        public enum ActionMode { Set, Restore }

        [Tooltip("If null, will try to find one in the scene.")] public CinemachinePositionComposer composer;

        public ActionMode action = ActionMode.Set;

        [Header("Set Values")]
        public float targetDistance = 22f;
        public Vector3 targetDamping = new Vector3(0.15f, 0.20f, 0.25f);

        [Header("Blend")]
        public float blendTime = 0.15f;
        public bool useUnscaledTime = true;

        private struct Baseline
        {
            public float distance;
            public Vector3 damping;
        }

        private static readonly Dictionary<int, Baseline> BaselineByComposer = new();
        private static Coroutine _activeCoroutine;

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
                _activeCoroutine = null;
            }

            int id = composer.GetInstanceID();

            if (action == ActionMode.Set)
            {
                if (!BaselineByComposer.ContainsKey(id))
                {
                    BaselineByComposer[id] = new Baseline
                    {
                        distance = composer.CameraDistance,
                        damping = composer.Damping
                    };
                }

                _activeCoroutine = runner.StartCoroutine(LerpComposer(
                    composer,
                    composer.CameraDistance, targetDistance,
                    composer.Damping, targetDamping,
                    blendTime));
            }
            else
            {
                if (!BaselineByComposer.TryGetValue(id, out var baseline))
                {
                    baseline = new Baseline { distance = composer.CameraDistance, damping = composer.Damping };
                }

                BaselineByComposer.Remove(id);

                _activeCoroutine = runner.StartCoroutine(LerpComposer(
                    composer,
                    composer.CameraDistance, baseline.distance,
                    composer.Damping, baseline.damping,
                    blendTime));
            }
        }

        private IEnumerator LerpComposer(CinemachinePositionComposer c, float distFrom, float distTo, Vector3 dampFrom, Vector3 dampTo, float time)
        {
            time = Mathf.Max(0.0001f, time);
            float t = 0f;

            while (t < 1f)
            {
                t += DeltaTime() / time;
                float k = Mathf.Clamp01(t);

                c.CameraDistance = Mathf.Lerp(distFrom, distTo, k);
                c.Damping = Vector3.Lerp(dampFrom, dampTo, k);

                yield return null;
            }

            _activeCoroutine = null;
        }

        private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
