using System.Collections;
using FeedbacksNagu;
using UnityEngine;

[System.Serializable][FeedbackCategory("Instance")]
public class FeedbackActivateGameObject : FeedbackBase
{
    public GameObject target;
    public bool setActive;
    public bool isPersistent;
    public float duration;
    
    public override void Play(GameObject owner)
    {
        target.SetActive(setActive);
        if (!isPersistent)
        {
            GameServices.Get<CoroutineRunner>().StartCoroutine(TimeToSwapObjectStatus());
        }
    }

    IEnumerator TimeToSwapObjectStatus()
    {
        yield return new WaitForSeconds(duration);
        target.SetActive(!setActive);
    }
}
