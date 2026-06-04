using UnityEngine;
using Unity.Cinemachine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Camera")]
    public class FeedbackCamaraShakeCinemachine : FeedbackBase
    {
        public float intensity = 1f;

        [Tooltip("Optional: override the source gains (leave <= 0 to keep source settings).")]
        public float amplitudeGainOverride = -1f;

        [Tooltip("Optional: override the source gains (leave <= 0 to keep source settings).")]
        public float frequencyGainOverride = -1f;

        [Tooltip("Optional: override duration (leave <= 0 to keep source settings).")]
        public float durationOverride = -1f;

        public CinemachineImpulseSource impulseSource;

        public override void Play(GameObject owner)
        {
            if (!active || impulseSource == null) return;

            // Keep shape/type as configured in the Impulse Source inspector.
            // Only override safe scalar parameters if requested.
            var def = impulseSource.ImpulseDefinition;

            if (durationOverride > 0f) def.ImpulseDuration = durationOverride;
            if (amplitudeGainOverride > 0f) def.AmplitudeGain = amplitudeGainOverride;
            if (frequencyGainOverride > 0f) def.FrequencyGain = frequencyGainOverride;

            impulseSource.ImpulseDefinition = def;

            impulseSource.GenerateImpulseWithForce(Mathf.Max(0f, intensity));
        }
    }
}