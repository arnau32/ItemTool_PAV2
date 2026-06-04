using System.Collections;
using System.Collections.Generic;
using Gameplay.Enemies;
using FeedbacksNagu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Monitors a list of pre-placed guard enemies.
/// When all guards are dead the extraction zone unlocks automatically — no player interaction required.
public class ExtractionPointInteractable : MonoBehaviour, ISaveable
{
    #region Fields

    [Header("Identity")] [SerializeField] private string _pointId;

    [Header("Guards")]
    [Tooltip("Pre-placed enemies that must be killed to unlock the extraction zone.")]
    [SerializeField] private List<EnemyBase> _guardEnemies;

    [Header("References")]
    [SerializeField] private GameObject _progressBarGO;
    [SerializeField] private CanvasGroup _progressCanvasGroup;
    [SerializeField] private float _progressFadeOutDuration = 1f;
    [SerializeField] private GameObject _unlockedAnnouncer;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private GameObject _extractionZone;
    [SerializeField] private FeedbackContainer _onUnlockedFeedback;

    private readonly HashSet<EnemyBase> _livingEnemies = new HashSet<EnemyBase>();
    private int _totalGuards;
    private bool _isActivated;
    private EnemyManager _enemyManager;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);
    }

    private void Start()
    {
        if (_isActivated) return;

        if (!GameServices.TryGet(out _enemyManager))
        {
            Debug.LogError($"ExtractionPointInteractable [{_pointId}]: EnemyManager not found.");
            return;
        }

        _totalGuards = 0;

        for (int i = 0; i < _guardEnemies.Count; i++)
        {
            var enemy = _guardEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            _livingEnemies.Add(enemy);
            _totalGuards++;
        }

        if (_totalGuards == 0)
        {
            ActivateZone(saveState: false);
            return;
        }

        _enemyManager.OnEnemyDied += HandleEnemyDied;

        if (_progressBarGO != null)
        {
            _progressBarGO.SetActive(true);
            RefreshProgressBar();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromManager();

        if (GameServices.TryGet<SaveService>(out var save))
            save.UnregisterSaveable(this);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_pointId)) return;

        var entries = data.extractionPoints.entries;
        ExtractionPointSaveEntry entry = null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].pointId == _pointId)
            {
                entry = entries[i];
                break;
            }
        }

        if (entry == null)
        {
            entry = new ExtractionPointSaveEntry { pointId = _pointId };
            entries.Add(entry);
        }

        entry.isActivated = _isActivated;
    }

    public void ApplyFromSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_pointId)) return;

        var entries = data.extractionPoints.entries;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].pointId != _pointId) continue;

            if (entries[i].isActivated)
                ActivateZone(saveState: false);

            return;
        }
    }

    #endregion

    #region Private

    private void HandleEnemyDied(EnemyBase enemy)
    {
        if (!_livingEnemies.Remove(enemy)) return;

        RefreshProgressBar();

        if (_livingEnemies.Count == 0)
            ActivateZone(saveState: true);
    }

    private void RefreshProgressBar()
    {
        if (_progressFill == null) return;

        int killed = _totalGuards - _livingEnemies.Count;
        _progressFill.fillAmount = (float)killed / _totalGuards;

        if (_progressText != null)
            _progressText.text = $"{killed} / {_totalGuards}";
    }

    private void ActivateZone(bool saveState)
    {
        UnsubscribeFromManager();

        _isActivated = true;

        if (_extractionZone != null)
            _extractionZone.SetActive(true);

        if (saveState && _unlockedAnnouncer != null)
            _unlockedAnnouncer.SetActive(true);

        if (_progressCanvasGroup != null && _progressFadeOutDuration > 0f)
            StartCoroutine(FadeOutProgressThenHide());
        else if (_progressBarGO != null)
            _progressBarGO.SetActive(false);

        if (!saveState) return;

        _onUnlockedFeedback?.PlayFeedbacks(gameObject);

        if (GameServices.TryGet<SaveService>(out var save))
            save.Save();
    }

    private IEnumerator FadeOutProgressThenHide()
    {
        _progressCanvasGroup.alpha = 1f;
        float elapsed = 0f;

        while (elapsed < _progressFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _progressCanvasGroup.alpha = 1f - (elapsed / _progressFadeOutDuration);
            yield return null;
        }

        _progressCanvasGroup.alpha = 0f;

        if (_progressBarGO != null)
            _progressBarGO.SetActive(false);
    }

    private void UnsubscribeFromManager()
    {
        if (_enemyManager != null)
            _enemyManager.OnEnemyDied -= HandleEnemyDied;

        _enemyManager = null;
    }

    #endregion
}
