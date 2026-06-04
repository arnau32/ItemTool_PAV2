using UnityEngine;

public class TabVisual : MonoBehaviour
{
    public GameObject selectedVisual;

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }
}