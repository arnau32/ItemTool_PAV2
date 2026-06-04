using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    [SerializeField] private UISounds _uiSounds;

    private AudioService _audio;

    #region Unity Callbacks

    private void Start()
    {
        GameServices.TryGet(out _audio);
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveListener(OnClick);
    }

    #endregion

    private void OnClick()
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || _uiSounds == null || _uiSounds.ButtonClick.IsNull) return;
        _audio.PlayOneShot(_uiSounds.ButtonClick, Vector3.zero);
    }
}
