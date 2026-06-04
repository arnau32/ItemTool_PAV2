using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ExtractionZone : TriggerPlayer
{
    //TODO: If player is in combat doesn't extract

    #region Fields

    [SerializeField] private GameObject            _extractionBar;
    [SerializeField] private Image                 _extractionProgress;
    [SerializeField] private float                 _timeToExtract;
    [SerializeField] private string                _sceneID;
    [SerializeField] private int                   _loadingScreenId = -1;
    [SerializeField] private ExtractionResumeScreen _resumeScreen;
    [SerializeField] private UnityEvent             _onExtract;
    [SerializeField] private PlayerHealthSystem    _playerHealth;

    private float _currentTime;
    private bool  _isPlayerInExtractZone;
    private bool  _extractionTriggered;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _extractionProgress.fillAmount = 0f;
    }

    private void Update()
    {
        if (!_isPlayerInExtractZone || _extractionTriggered) return;

        _currentTime += Time.deltaTime;
        _extractionProgress.fillAmount = Mathf.Clamp(_currentTime / _timeToExtract, 0f, 1f);

        if (_currentTime >= _timeToExtract)
        {
            _extractionTriggered = true;
            StartExtraction();
        }
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        _isPlayerInExtractZone = true;
        _extractionBar.SetActive(true);
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        _isPlayerInExtractZone = false;
        _extractionBar.SetActive(false);
        _extractionProgress.fillAmount = 0f;
        _currentTime = 0;
    }

    #endregion

    #region Private

    private void StartExtraction()
    {
        _onExtract?.Invoke();

        float lootVal = CalculateTotalLootValue();
        CombatAnalytics.ExtractionRunEnded(true, lootVal, lootVal, "", "", Vector3.zero);
        CombatAnalytics.EndSession("extracted");

        if (GameServices.TryGet<QuestService>(out var qs))
            qs.ReportExtractionSuccess();

        if (GameServices.TryGet<MorranContractService>(out var morran))
            morran.ReportExtractionSuccess();

        PlayerInventory.Instance?.ExtractAuroraDustCollectables();
        RespawnData.SpawnPointIndex  = 2;
        RespawnData.UseSavedPosition = false;

        if (_resumeScreen != null)
        {
            _resumeScreen.Show(_sceneID, _loadingScreenId);
            return;
        }

        // Resume screen handles invulnerability via its OnEnable.
        // When there is no resume screen, grant it manually for the load window.
        if (_playerHealth != null)
            _playerHealth.SetInvulnerable(true);

        var loader = GameServices.Get<ISceneLoader>();
        if (_loadingScreenId >= 0)
            loader.LoadScene(_sceneID, 3.5f, _loadingScreenId);
        else
            loader.LoadScene(_sceneID, 3.5f);
    }

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

    #endregion
}