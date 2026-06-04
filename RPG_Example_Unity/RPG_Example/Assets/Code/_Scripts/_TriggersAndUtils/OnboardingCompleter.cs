using UnityEngine;

public class OnboardingCompleter : MonoBehaviour
{
    public void Complete()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        save.CurrentSave.meta.onboardingCompleted = true;
        save.Save();
    }
}