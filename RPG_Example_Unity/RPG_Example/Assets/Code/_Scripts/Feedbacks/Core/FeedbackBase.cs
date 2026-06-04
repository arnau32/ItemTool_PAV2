using UnityEngine;

namespace FeedbacksNagu
{
    [System.Serializable]
    public abstract class FeedbackBase
    {
        public bool active = true;
        public abstract void Play(GameObject owner);
    }
}

