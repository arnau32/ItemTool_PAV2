using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    public class FeedbackCameraFovStateCinemachine : FeedbackBase
    {
        public enum ActionMode { Set, Restore }

        [Tooltip("If null, will try to find the active CinemachineCamera in the scene.")]
        public CinemachineCamera cmCamera;

        public ActionMode action = ActionMode.Set;

        [Tooltip("Target FOV for Set mode (zoom in). For Restore mode, set this to the original camera FOV you want to return to.")]
        public float targetFov = 40f;

        [Header("Blend")]
        public float blendTime = 0.15f;
        public bool useUnscaledTime = true;

        private static Coroutine _activeCoroutine;

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
                _activeCoroutine = null;
            }

            _activeCoroutine = runner.StartCoroutine(LerpFov(cmCamera, cmCamera.Lens.FieldOfView, targetFov, blendTime));
        }

        private IEnumerator LerpFov(CinemachineCamera cam, float from, float to, float time)
        {
            time = Mathf.Max(0.0001f, time);
            float t = 0f;

            while (t < 1f)
            {
                t += DeltaTime() / time;
                float v = Mathf.Lerp(from, to, Mathf.Clamp01(t));

                var lens = cam.Lens;
                lens.FieldOfView = v;
                cam.Lens = lens;

                yield return null;
            }

            _activeCoroutine = null;
        }

        private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
