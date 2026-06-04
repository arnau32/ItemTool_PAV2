using UnityEngine;
using UnityEngine.InputSystem;

public class MapPanelActive : MonoBehaviour
{
    public static MapPanelActive Instance;

    [SerializeField] private UISounds _uiSounds;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    public void OnOpenMapPanel(InputAction.CallbackContext ctx)
    {
        if (TabViewManager.Instance != null && TabViewManager.Instance.isActive) return;

        if (WorldMapController.Instance == null || WorldMapController.Instance.IsOpen) return;

        WorldMapController.Instance.SetMapOpen(true);
        GameServices.Get<InputService>().OnUIMapOpen();
        
        PlayUISound(_uiSounds?.MapOpen ?? default);
    }

    public void OnCloseMapPanel(InputAction.CallbackContext ctx)
    {
        if (WorldMapController.Instance == null || !WorldMapController.Instance.IsOpen) return;

        WorldMapController.Instance.SetMapOpen(false);
        GameServices.Get<InputService>().OnUIMapClose();
    }

    private void PlayUISound(FMODUnity.EventReference sound)
    {
        if (sound.IsNull) return;
        GameServices.Get<AudioService>().PlayOneShot(sound, Vector3.zero);
    }
}
