using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class SkillTreeUI : MonoBehaviour
{
    #region Fields

    public static event Action OnOpened;
    public static event Action OnClosed;

    [Header("Upgrade Buttons")]
    [SerializeField] private UpgradeButtonUI _upgradeButtonPrefab;
    [SerializeField] private Transform _buttonContainer;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _dustLabel;

    private PlayerUpgradeService _service;
    private InputService _inputService;
    private UpgradeButtonUI[] _spawnedButtons;

    private bool _isOpen;
    private UnityEngine.UI.Button _defaultSelectedButton;
    private GameObject _lastSelectedObject;
    private Coroutine _selectRoutine;

    private PanelEventHandler[] _panelEventHandlers;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _inputService = GameServices.Get<InputService>();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isOpen || EventSystem.current == null) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (IsValidSelectable(current))
        {
            _lastSelectedObject = current;
            return;
        }

        if (IsValidSelectable(_lastSelectedObject))
        {
            EventSystem.current.SetSelectedGameObject(_lastSelectedObject);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!_isOpen || !hasFocus) return;

        StartSelectNextFrame();
    }

    private void OnEnable()
    {
        if (_isOpen)
            StartSelectNextFrame();
    }

    private void OnDisable()
    {
        StopSelectRoutine();
    }

    private void OnDestroy()
    {
        StopSelectRoutine();
        UnsubscribeCancel();

        if (_service != null)
            _service.OnUpgradesChanged -= RefreshButtons;

    }

    #endregion

    #region Public API

    public void Open(PlayerUpgradeService service)
    {
        if (service == null)
        {
            Debug.LogError("[SkillTreeUI] Open recibió un PlayerUpgradeService null.");
            return;
        }

        if (_service != null)
            _service.OnUpgradesChanged -= RefreshButtons;

        _service = service;
        _service.OnUpgradesChanged += RefreshButtons;

        _isOpen = true;
        gameObject.SetActive(true);

        OnOpened?.Invoke();
        SetPanelEventHandlersEnabled(false);

        SpawnButtons();
        RefreshAll();

        if (_inputService != null)
            _inputService.Actions.UI.Cancel.performed += HandleCancel;
        else
            Debug.LogError("[SkillTreeUI] InputService es null.");

        _defaultSelectedButton = GetFirstValidButton();
        _lastSelectedObject = GetBestInitialSelection();

        StartSelectNextFrame();
    }

    public void Close()
    {
        StopSelectRoutine();
        UnsubscribeCancel();

        if (_service != null)
        {
            _service.OnUpgradesChanged -= RefreshButtons;
            _service = null;
        }

        DestroyButtons();
        SetPanelEventHandlersEnabled(true);

        _isOpen = false;
        gameObject.SetActive(false);

        OnClosed?.Invoke();
        GameServices.Get<InputService>()?.OnUIClose();
    }

    #endregion

    #region Spawn

    private void SpawnButtons()
    {
        DestroyButtons();

        if (_upgradeButtonPrefab == null || _buttonContainer == null || _service == null) return;

        int count = _service.NodeCount;
        _spawnedButtons = new UpgradeButtonUI[count];

        for (int i = 0; i < count; i++)
        {
            UpgradeButtonUI instance = Instantiate(_upgradeButtonPrefab, _buttonContainer);
            instance.Bind(_service, i);
            _spawnedButtons[i] = instance;
        }
    }

    private void DestroyButtons()
    {
        if (_spawnedButtons == null) return;

        for (int i = 0; i < _spawnedButtons.Length; i++)
        {
            if (_spawnedButtons[i] != null)
                Destroy(_spawnedButtons[i].gameObject);
        }

        _spawnedButtons = null;
    }

    #endregion

    #region Refresh

    private void RefreshAll()
    {
        RefreshDustLabel();
        RefreshButtons();
    }

    private void RefreshDustLabel()
    {
        _dustLabel.text = PlayerInventory.Instance.AuraDust.ToString();
    }

    private void OnAuraDustNameChanged(string localized)
    {
        if (_isOpen)
        {
            RefreshDustLabel();
        }
    }

    private void RefreshButtons()
    {
        RefreshDustLabel();

        if (_spawnedButtons == null) return;

        for (int i = 0; i < _spawnedButtons.Length; i++)
        {
            if (_spawnedButtons[i] != null)
                _spawnedButtons[i].Refresh();
        }

        _defaultSelectedButton = GetFirstValidButton();

        if (!IsValidSelectable(_lastSelectedObject))
            _lastSelectedObject = GetBestInitialSelection();
    }

    #endregion

    #region Input

    private void HandleCancel(InputAction.CallbackContext ctx)
    {
        Close();
    }

    private void UnsubscribeCancel()
    {
        if (_inputService != null)
            _inputService.Actions.UI.Cancel.performed -= HandleCancel;
    }

    #endregion

    #region Selection

    private void SetPanelEventHandlersEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_panelEventHandlers == null) return;

            foreach (var handler in _panelEventHandlers)
            {
                if (handler != null)
                    handler.enabled = true;
            }

            _panelEventHandlers = null;
            return;
        }

        _panelEventHandlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);

        foreach (var handler in _panelEventHandlers)
        {
            if (handler != null)
                handler.enabled = false;
        }
    }

    private void StartSelectNextFrame()
    {
        StopSelectRoutine();
        _selectRoutine = StartCoroutine(SelectNextFrameRoutine());
    }

    private void StopSelectRoutine()
    {
        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }
    }

    private IEnumerator SelectNextFrameRoutine()
    {
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        if (!_isOpen || !gameObject.activeInHierarchy || EventSystem.current == null)
        {
            _selectRoutine = null;
            yield break;
        }

        Canvas.ForceUpdateCanvases();

        GameObject target = GetBestInitialSelection();

        if (!IsValidSelectable(target))
        {
            _selectRoutine = null;
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);

        Selectable selectable = target.GetComponent<Selectable>();
        if (selectable != null)
            selectable.Select();

        _lastSelectedObject = target;
        _selectRoutine = null;
    }

    private GameObject GetBestInitialSelection()
    {
        if (IsValidSelectable(_lastSelectedObject)) return _lastSelectedObject;

        if (_defaultSelectedButton != null && IsValidSelectable(_defaultSelectedButton.gameObject)) return _defaultSelectedButton.gameObject;

        return null;
    }

    private UnityEngine.UI.Button GetFirstValidButton()
    {
        if (_spawnedButtons != null)
        {
            for (int i = 0; i < _spawnedButtons.Length; i++)
            {
                UnityEngine.UI.Button btn = _spawnedButtons[i]?.Button;

                if (btn != null && btn.gameObject.activeInHierarchy && btn.IsInteractable()) return btn;
            }
        }

        return null;
    }

    private bool IsValidSelectable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;

        Selectable selectable = go.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }

    #endregion
}
