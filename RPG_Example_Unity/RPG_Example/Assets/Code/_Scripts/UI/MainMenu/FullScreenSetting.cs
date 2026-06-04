using UnityEngine;
using UnityEngine.UI;

public class FullScreenSetting : MonoBehaviour
{
    public Toggle fullscreenToggle;

    void Start()
    {
        fullscreenToggle.isOn = Screen.fullScreen;

        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    void OnFullscreenChanged(bool isOn)
    {
        Screen.fullScreen = isOn;
    }
}
