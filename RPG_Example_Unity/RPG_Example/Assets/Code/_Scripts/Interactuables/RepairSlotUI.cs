using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RepairSlotUI : MonoBehaviour
{
    #region Fields

    private static readonly Color COLOR_AVAILABLE = Color.white;
    private static readonly Color COLOR_MISSING   = new Color(1f, 0.3f, 0.3f, 1f);

    [SerializeField] private Image    _icon;
    [SerializeField] private TMP_Text _countText;

    #endregion

    #region Properties

    public Image    Icon      => _icon;
    public TMP_Text CountText => _countText;

    #endregion

    #region Public API

    public void SetAvailable(bool available)
    {
        if (_icon != null)
            _icon.color = available ? COLOR_AVAILABLE : COLOR_MISSING;
    }

    #endregion
}
