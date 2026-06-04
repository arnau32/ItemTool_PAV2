using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ToggleRowController : MonoBehaviour, IMoveHandler, ISubmitHandler
{
    public Toggle toggle;

    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;

    private void Start()
    {
        GameServices.TryGet(out _audio);
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left ||
            eventData.moveDir == MoveDirection.Right)
        {
            ToggleValue();
            eventData.Use();
            PlaySliderSound();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ToggleValue();
        PlaySliderSound();
    }

    private void ToggleValue()
    {
        toggle.isOn = !toggle.isOn;
    }

    private void PlaySliderSound()
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.SliderMove.IsNull) return;
        _audio.PlayOneShot(_uiSounds.SliderMove, Vector3.zero);
    }
}
