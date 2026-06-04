using FMODUnity;
using UnityEngine;

namespace FeedbacksNagu
{
    [System.Serializable][FeedbackCategory("Audio")]
    public class FeedbackPlayAudioChance : FeedbackBase
    {
        public EventReference sound;

        [Tooltip("Probability (0-1) that the sound fires. 0 = never, 1 = always.")]
        public AudioChanceFilter filter = new AudioChanceFilter(0.4f, 1.0f);

        public override void Play(GameObject owner)
        {
            if (!active || sound.IsNull) return;
            if (!filter.Roll()) return;

            var pos = owner != null ? owner.transform.position : Vector3.zero;
            RuntimeManager.PlayOneShot(sound, pos);
        }
    }
}