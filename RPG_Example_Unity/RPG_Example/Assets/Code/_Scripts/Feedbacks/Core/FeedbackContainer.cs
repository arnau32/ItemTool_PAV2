using UnityEngine;
using System.Collections.Generic;

namespace FeedbacksNagu
{
    [System.Serializable]
    public class FeedbackContainer
    {
        [SerializeReference] public List<FeedbackBase> feedbacks = new List<FeedbackBase>();

        public void PlayFeedbacks(GameObject owner)
        {
            if (owner == null || feedbacks == null) return;

            int count = feedbacks.Count;
            for (int i = 0; i < count; i++)
            {
                var fb = feedbacks[i];
                if (fb == null || !fb.active) continue;
                fb.Play(owner);
            }
        }
    }
}