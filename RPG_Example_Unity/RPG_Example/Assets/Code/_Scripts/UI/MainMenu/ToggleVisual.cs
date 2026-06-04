using UnityEngine;
using UnityEngine.UI;

public class ToggleVisual : MonoBehaviour
{
    public Toggle toggle;
    public GameObject onVisual;

    void Start()
    {
        UpdateVisual(toggle.isOn);

        toggle.onValueChanged.AddListener(UpdateVisual);
    }

    void UpdateVisual(bool isOn)
    {
        if (onVisual != null)
        onVisual.SetActive(isOn);

    }
}