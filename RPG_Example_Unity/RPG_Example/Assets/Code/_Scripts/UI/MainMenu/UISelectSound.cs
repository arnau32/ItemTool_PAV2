using UnityEngine;
using UnityEngine.EventSystems;

public class UISelectSound : MonoBehaviour, ISelectHandler
{
    [SerializeField] private UISounds _uiSounds;

    private AudioService _audio;

    private void Start()
    {
        GameServices.TryGet(out _audio);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.MenuNavigate.IsNull) return;
        _audio.PlayOneShot(_uiSounds.MenuNavigate, Vector3.zero);
    }
}
