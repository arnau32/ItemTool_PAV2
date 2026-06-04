using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class OptionSelector : MonoBehaviour, IMoveHandler, ISelectHandler
{
    public TextMeshProUGUI valueText;

    public string[] options;
    private int index = 0;

    public System.Action<int> OnValueChanged;

    [SerializeField] private UISounds _uiSounds;
    private AudioService _audio;

    private void Start()
    {
        GameServices.TryGet(out _audio);
    }

    public void SetOptions(string[] newOptions, int startIndex = 0)
    {
        options = newOptions;

        if (options == null || options.Length == 0)
            return;

        index = Mathf.Clamp(startIndex, 0, options.Length - 1);

        UpdateUI(false);
    }

    public void Next()
    {
        index = (index + 1) % options.Length;
        UpdateUI(true);
    }

    public void Prev()
    {
        index = (index - 1 + options.Length) % options.Length;
        UpdateUI(true);
    }

    void UpdateUI(bool notify)
    {
        valueText.text = options[index];

        if (notify)
            OnValueChanged?.Invoke(index);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.MenuNavigate.IsNull) return;
        _audio.PlayOneShot(_uiSounds.MenuNavigate, Vector3.zero);
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Right)
        {
            Next();
            eventData.Use();
            PlaySliderSound();
        }
        else if (eventData.moveDir == MoveDirection.Left)
        {
            Prev();
            eventData.Use();
            PlaySliderSound();
        }
    }

    public void SetIndexWithoutNotify(int newIndex)
    {
        index = newIndex;
        UpdateUI(false);
    }

    private void PlaySliderSound()
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.SliderMove.IsNull) return;
        _audio.PlayOneShot(_uiSounds.SliderMove, Vector3.zero);
    }
}
