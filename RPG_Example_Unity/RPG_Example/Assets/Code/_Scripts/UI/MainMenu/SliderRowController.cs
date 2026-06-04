using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SliderRowController : MonoBehaviour, IMoveHandler
{
    public Slider slider;
    public float step = 0.1f;

    [SerializeField] private UISounds _uiSounds;

    private AudioService _audio;

    private void Start()
    {
        GameServices.TryGet(out _audio);
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left)
        {
            slider.value -= step;
            eventData.Use();
            PlaySliderSound();
        }
        else if (eventData.moveDir == MoveDirection.Right)
        {
            slider.value += step;
            eventData.Use();
            PlaySliderSound();
        }
    }

    private void PlaySliderSound()
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.SliderMove.IsNull) return;
        _audio.PlayOneShot(_uiSounds.SliderMove, Vector3.zero);
    }
}
