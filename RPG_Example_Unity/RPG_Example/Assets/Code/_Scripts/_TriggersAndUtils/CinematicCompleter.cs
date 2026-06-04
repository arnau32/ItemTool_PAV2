using UnityEngine;

public class CinematicCompleter : MonoBehaviour
{
    public void Complete()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        save.CurrentSave.meta.cinematicCompleted = true;
        save.Save();
    }
}