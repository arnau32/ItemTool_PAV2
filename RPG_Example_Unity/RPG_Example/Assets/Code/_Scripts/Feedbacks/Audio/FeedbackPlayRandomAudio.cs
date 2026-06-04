using System;
using FMODUnity;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FeedbacksNagu
{
    [Serializable][FeedbackCategory("Audio")]
    public class FeedbackPlayRandomAudio : FeedbackBase
    {
        // Pool of FMOD events to randomly pick from.
        // Requires at least 2 entries to benefit from shuffle behaviour.
        public EventReference[] sounds = Array.Empty<EventReference>();

        // Shuffle bag state — not serialized, lives only at runtime.
        [NonSerialized] private int[] _shuffledIndices;
        [NonSerialized] private int   _currentIndex = -1;
        [NonSerialized] private bool  _initialized;

        public override void Play(GameObject owner)
        {
            if (!active || sounds == null || sounds.Length == 0) return;

            var sound = GetNext();
            if (sound.IsNull) return;

            var pos = owner != null ? owner.transform.position : Vector3.zero;
            RuntimeManager.PlayOneShot(sound, pos);
        }

        // Returns the next non-repeating EventReference from the shuffled pool.
        private EventReference GetNext()
        {
            int count = sounds.Length;

            if (count == 1) return sounds[0];

            if (!_initialized || _shuffledIndices == null || _shuffledIndices.Length != count)
                InitializeShuffleBag(count);

            _currentIndex++;

            // Reshuffle when pool is exhausted, ensuring the wrap-around
            // doesn't immediately repeat the last played sound.
            if (_currentIndex >= count)
            {
                int lastPlayed = _shuffledIndices[count - 1];
                ShuffleInPlace(_shuffledIndices);

                // If the new first element equals the last played, swap it
                // with any other position to avoid immediate repetition.
                if (count > 1 && _shuffledIndices[0] == lastPlayed)
                {
                    int swapWith = Random.Range(1, count);
                    (_shuffledIndices[0], _shuffledIndices[swapWith]) =
                        (_shuffledIndices[swapWith], _shuffledIndices[0]);
                }

                _currentIndex = 0;
            }

            return sounds[_shuffledIndices[_currentIndex]];
        }

        private void InitializeShuffleBag(int count)
        {
            _shuffledIndices = new int[count];
            for (int i = 0; i < count; i++) _shuffledIndices[i] = i;
            ShuffleInPlace(_shuffledIndices);
            _currentIndex = -1;
            _initialized  = true;
        }

        // Fisher-Yates shuffle — O(n), zero allocations.
        private static void ShuffleInPlace(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

#if UNITY_EDITOR
        // Same approach as FeedbackPlayAudio.PreviewPlay — uses EditorUtils.System
        // instead of RuntimeManager to avoid the "accessed outside of runtime" error.
        public void PreviewPlay()
        {
            if (sounds == null || sounds.Length == 0) return;

            var sound = GetNext();
            if (sound.IsNull) return;

            var result = FMODUnity.EditorUtils.System.getEventByID(sound.Guid, out var desc);
            if (result != FMOD.RESULT.OK) return;

            desc.createInstance(out var instance);
            instance.start();
            instance.release();
        }
#endif
    }
}