using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

public class DoorExtractionZone : TriggerPlayer
{
    #region Fields

    [Header("Hold to Extract")]
    [SerializeField] private GameObject _extractionBar;
    [SerializeField] private Image _extractionProgress;
    [SerializeField] private float _timeToExtract = 3f;

    [Header("No Dust Feedback")]
    [SerializeField] private GameObject _noDustMessage;
    [SerializeField] private float _noDustMessageDuration = 2f;

    [Header("Extraction Label")]
    [SerializeField] private TMP_Text      _extractionLabel;
    [SerializeField] private LocalizedString _labelPay;
    [SerializeField] private LocalizedString _labelMinimum;
    [SerializeField] private int _minimumAuroraDust = 25;

    [Header("Extraction")]
    [SerializeField] private string _sceneID;
    [SerializeField] private int    _loadingScreenId = -1;
    [SerializeField] private ExtractionResumeScreen _resumeScreen;
    [SerializeField] private PlayerHealthSystem _playerHealth;

    private bool _playerInZone;
    private bool _extractionTriggered;
    private bool _wasHolding;
    private bool _showingNoDustMessage;
    private float _currentTime;
    private float _noDustMessageTimer;

    private string _cachedLabelPay     = "";
    private string _cachedLabelMinimum = "";

    private InputAction _interactAction;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _interactAction = GameServices.Get<InputService>().Actions.Player.Interact;
        _extractionProgress.fillAmount = 0f;

        if (_noDustMessage != null)
            _noDustMessage.SetActive(false);
    }

    private void OnEnable()
    {
        if (_labelPay != null)     _labelPay.StringChanged     += OnPayLabelChanged;
        if (_labelMinimum != null) _labelMinimum.StringChanged += OnMinimumLabelChanged;
    }

    private void OnDisable()
    {
        if (_labelPay != null)     _labelPay.StringChanged     -= OnPayLabelChanged;
        if (_labelMinimum != null) _labelMinimum.StringChanged -= OnMinimumLabelChanged;
    }

    private void Update()
    {
        TickNoDustMessage();

        if (!_playerInZone || _extractionTriggered) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

        bool isHolding = _interactAction.IsPressed();

        if (isHolding && !_wasHolding && !CanExtract())
        {
            ShowNoDustMessage();
            _wasHolding = true;
            return;
        }

        _wasHolding = isHolding;

        if (!isHolding)
        {
            if (_currentTime > 0f) ResetProgress();
            return;
        }

        if (!CanExtract())
        {
            ResetProgress();
            return;
        }

        _currentTime += Time.deltaTime;
        _extractionProgress.fillAmount = Mathf.Clamp01(_currentTime / _timeToExtract);

        if (_currentTime >= _timeToExtract)
        {
            _extractionTriggered = true;
            StartExtraction();
        }
    }

    #endregion

    #region TriggerPlayer

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        _playerInZone = true;
        _extractionBar.SetActive(true);
        RefreshExtractionLabel();
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        _playerInZone = false;
        _wasHolding = false;
        _extractionBar.SetActive(false);
        ResetProgress();
        HideNoDustMessage();
    }

    #endregion

    #region Private

    private void TickNoDustMessage()
    {
        if (!_showingNoDustMessage) return;

        _noDustMessageTimer -= Time.deltaTime;
        if (_noDustMessageTimer <= 0f)
            HideNoDustMessage();
    }

    private void ShowNoDustMessage()
    {
        if (_noDustMessage == null) return;
        _showingNoDustMessage = true;
        _noDustMessageTimer = _noDustMessageDuration;
        _noDustMessage.SetActive(true);
    }

    private void HideNoDustMessage()
    {
        _showingNoDustMessage = false;
        if (_noDustMessage != null)
            _noDustMessage.SetActive(false);
    }

    private void OnPayLabelChanged(string value)
    {
        _cachedLabelPay = value;
        RefreshExtractionLabel();
    }

    private void OnMinimumLabelChanged(string value)
    {
        _cachedLabelMinimum = value;
        RefreshExtractionLabel();
    }

    private void RefreshExtractionLabel()
    {
        if (_extractionLabel == null) return;
        int penalty = CalculatePenalty();
        _extractionLabel.text = $"<color=red>{_cachedLabelPay}{penalty}</color> ({_cachedLabelMinimum}{_minimumAuroraDust})";
    }

    private bool CanExtract() => GetTotalAuroraDust() >= _minimumAuroraDust;

    private static float CalculateTotalLootValue()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return 0f;
        float total = 0f;
        var stacks = inv.ItemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data != null)
                total += stacks[i].data.value * stacks[i].quantity;
        }
        return total;
    }

    private int GetTotalAuroraDust()
    {
        var stacks = PlayerInventory.Instance.ItemStacks;
        int total = 0;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].data is CollectableItemData c && c.isAuroraDust)
                total += stacks[i].quantity;
        }
        return total;
    }

    private int CalculatePenalty()
    {
        int total = GetTotalAuroraDust();
        if (total < _minimumAuroraDust) return 0;
        return Mathf.Max(_minimumAuroraDust, total / 2);
    }

    private void ResetProgress()
    {
        _currentTime = 0f;
        _extractionProgress.fillAmount = 0f;
    }

    private void StartExtraction()
    {
        float lootVal = CalculateTotalLootValue();
        CombatAnalytics.ExtractionRunEnded(true, lootVal, lootVal, "", "", Vector3.zero);
        CombatAnalytics.EndSession("extracted_door");

        int penalty = CalculatePenalty();

        if (GameServices.TryGet<QuestService>(out var qs))
            qs.ReportExtractionSuccess(penalty);
        RespawnData.SpawnPointIndex  = 1;
        RespawnData.UseSavedPosition = false;

        if (_resumeScreen != null)
        {
            _resumeScreen.SetPenalty(penalty);
            _resumeScreen.Show(_sceneID, _loadingScreenId);
            return;
        }

        // Resume screen handles invulnerability via its OnEnable.
        // When there is no resume screen, grant it manually for the load window.
        if (_playerHealth != null)
            _playerHealth.SetInvulnerable(true);

        PlayerInventory.Instance.ConsumeDustPenalty(penalty);
        PlayerInventory.Instance.ExtractAuroraDustCollectables();

        var loader = GameServices.Get<ISceneLoader>();
        if (_loadingScreenId >= 0)
            loader.LoadScene(_sceneID, 3.5f, _loadingScreenId);
        else
            loader.LoadScene(_sceneID, 3.5f);
    }

    #endregion
}
