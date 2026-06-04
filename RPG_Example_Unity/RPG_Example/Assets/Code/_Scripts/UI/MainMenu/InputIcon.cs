using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InputIcon : MonoBehaviour
{
    public InputIconType iconType;

    private Image             _image;
    private InputIconSettings _inputIcons;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void Start()
    {
        _inputIcons = GameServices.Get<InputIconSettings>();

        Refresh();
        _inputIcons.OnInputDeviceChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (_inputIcons != null)
            _inputIcons.OnInputDeviceChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_inputIcons == null)
            _inputIcons = GameServices.Get<InputIconSettings>();

        var db = _inputIcons.CurrentIconDatabase;
        if (db == null)
        {
            Debug.LogWarning("[InputIcon] No IconDatabase assigned.");
            return;
        }

        var sprite = db.Get(iconType);
        if (sprite == null)
        {
            Debug.LogWarning($"[InputIcon] No sprite found for {iconType}");
            return;
        }

        _image.sprite = sprite;
    }
}