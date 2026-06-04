using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("SpecialEffects")]
    public class FeedbackRumble : FeedbackBase
    {
        [Range(0f, 1f)] public float lowFrequency = 0.25f;
        [Range(0f, 1f)] public float highFrequency = 0.75f;
        public float duration = 0.08f;

        // If true, uses unscaled time so rumble still plays during slow motion / pause
        public bool useUnscaledTime = true;

        private static Coroutine _activeCoroutine;

        public override void Play(GameObject owner)
        {
            if (!active) return;

            var gamepad = Gamepad.current;
            if (gamepad == null) return;

            var runner = GameServices.Get<CoroutineRunner>();
            if (runner == null) return;

            // Replace previous rumble to avoid stacking noise
            if (_activeCoroutine != null)
            {
                runner.StopCoroutine(_activeCoroutine);
                StopRumble(gamepad);
                _activeCoroutine = null;
            }

            gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
            _activeCoroutine = runner.StartCoroutine(RumbleCoroutine(gamepad));
        }

        private IEnumerator RumbleCoroutine(Gamepad gamepad)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(duration);
            else
                yield return new WaitForSeconds(duration);

            StopRumble(gamepad);
            _activeCoroutine = null;
        }

        private void StopRumble(Gamepad gamepad)
        {
            if (gamepad == null) return;
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }
}