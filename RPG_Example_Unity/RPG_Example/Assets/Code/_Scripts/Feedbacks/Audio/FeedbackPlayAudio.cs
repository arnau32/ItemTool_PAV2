using System;
using FMODUnity;
using UnityEngine;

namespace FeedbacksNagu
{
    [Serializable][FeedbackCategory("Audio")]
    public class FeedbackPlayAudio : FeedbackBase
    {
        public EventReference sound;

        public override void Play(GameObject owner)
        {
            if (!active || sound.IsNull) return;

            var pos = owner != null ? owner.transform.position : Vector3.zero;
            RuntimeManager.PlayOneShot(sound, pos);
        }

#if UNITY_EDITOR
        public void PreviewPlay()
        {
            if (sound.IsNull) return;

            var eventDesc = EditorUtils.System.getEventByID(sound.Guid, out var desc);
            if (eventDesc != FMOD.RESULT.OK) return;

            desc.createInstance(out var instance);
            instance.start();
            instance.release(); // Fire-and-forget: FMOD manages lifetime after release
        }
#endif
    }
}