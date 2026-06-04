using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class UpgradeButtonUI : MonoBehaviour
{
    #region Fields

    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _costLabel;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private UISounds _uiSounds;

    private PlayerUpgradeService _service;
    private int _index;
    private LocalizedString _boundDisplayName;
    private string _localizedDisplayName = string.Empty;
    private AudioService _audio;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        GameServices.TryGet(out _audio);
    }

    private void OnDestroy()
    {
        if (_boundDisplayName != null)
            _boundDisplayName.StringChanged -= OnDisplayNameChanged;
    }

    #endregion

    #region Public API

    public Button Button => _button;

    public void Bind(PlayerUpgradeService service, int index)
    {
        if (_boundDisplayName != null)
            _boundDisplayName.StringChanged -= OnDisplayNameChanged;

        _service = service;
        _index = index;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnPressed);

        _boundDisplayName = service.GetNode(index).displayName;
        _boundDisplayName.StringChanged += OnDisplayNameChanged;

        RefreshCostLabel();
    }

    public void Refresh()
    {
        if (_service == null) return;

        RefreshNameLabel();
        RefreshCostLabel();
    }

    #endregion

    #region Refresh

    private void OnDisplayNameChanged(string localized)
    {
        _localizedDisplayName = localized;
        RefreshNameLabel();
    }

    private void RefreshNameLabel()
    {
        if (_nameLabel == null || _service == null) return;

        var node = _service.GetNode(_index);
        int level = _service.GetLevel(_index);
        bool maxed = _service.IsMaxLevel(_index);

        string levelText = maxed ? " MAX" : $" Lv {level}/{node.maxLevel}";
        string incrementText = string.Empty;

        if (!maxed)
        {
            float nextIncrement = node.GetTotalValueAtLevel(level + 1) - node.GetTotalValueAtLevel(level);
            incrementText = $"\n +{nextIncrement:F0}";
        }

        _nameLabel.text = $"{_localizedDisplayName} ({levelText}){incrementText}";
    }

    private void RefreshCostLabel()
    {
        if (_service == null) return;

        var node = _service.GetNode(_index);
        bool maxed = _service.IsMaxLevel(_index);

        if (_costLabel != null)
            _costLabel.text = maxed ? "MAX" : $"{_service.GetCost(_index)} Dust";

        _button.interactable = !maxed;

        if (node.icon != null)
            _icon.sprite = node.icon;
    }

    #endregion

    #region Button Callback

    private void OnPressed()
    {
        bool upgraded = _service != null && _service.TryUpgrade(_index);
        if (upgraded)
            PlayUISound(_uiSounds.UpgradeComplete);
    }

    private void PlayUISound(FMODUnity.EventReference sound)
    {
        if (_audio == null || _uiSounds == null || sound.IsNull) return;
        _audio.PlayOneShot(sound, Vector3.zero);
    }

    #endregion
}
