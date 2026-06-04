using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseMenu : MonoBehaviour
{
    private UIDocument _document;
    private Button _continueButton;
    private Button _settingsButton;
    private Button _exitButton;
    public VisualElement _PauseMenuPanelRoot;

    [SerializeField] private UISounds _uiSounds;

    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;
    private AudioService _audio;

    public static PauseMenu Instance;

    private int _currentIndex = 0;
    private List<Button> _menuButtons = new List<Button>(); 
    private List<Action> _buttonActions = new List<Action>();

    private float _navCooldown = 0.25f;
    private float _nextNavTime = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }

    }
    void Start()
    {
        _input = GameServices.Get<InputService>();
        GameServices.TryGet(out _audio);

        _document = GetComponent<UIDocument>();
        _PauseMenuPanelRoot = _document.rootVisualElement.Q<VisualElement>("PauseMenuContent");
        _menuButtons = _PauseMenuPanelRoot.Query<Button>().ToList();

        _continueButton = _document.rootVisualElement.Q<Button>("ContinueButton");
        _settingsButton = _document.rootVisualElement.Q<Button>("SettingButton");
        _exitButton = _document.rootVisualElement.Q<Button>("ExitButton");

        _continueButton.clicked += HandleContinue;
        _settingsButton.clicked += HandleSettings;
        _exitButton.clicked += HandleExit;

        InputActions.UI_PauseMenu.Navigate.started += OnNavMove; 
        InputActions.UI_PauseMenu.Submit.performed += OnSubmit;

        _buttonActions.Add(HandleContinue);
        _buttonActions.Add(HandleSettings);
        _buttonActions.Add(HandleExit);

        _PauseMenuPanelRoot.RegisterCallback<ClickEvent>(OnAnyButtonClicked);

        InitPauseMenu();
    }

    private void OnDestroy()
    {
        _continueButton.clicked -= HandleContinue;
        _settingsButton.clicked -= HandleSettings;
        _exitButton.clicked -= HandleExit;

        InputActions.UI_PauseMenu.Navigate.started -= OnNavMove;
        InputActions.UI_PauseMenu.Submit.performed -= OnSubmit;

        _PauseMenuPanelRoot?.UnregisterCallback<ClickEvent>(OnAnyButtonClicked);
    }
    private void OnAnyButtonClicked(ClickEvent evt)
    {
        if (evt.target is not UnityEngine.UIElements.Button) return;
        if (_audio == null) GameServices.TryGet(out _audio);
        if (_audio == null || _uiSounds == null || _uiSounds.ButtonClick.IsNull) return;
        _audio.PlayOneShot(_uiSounds.ButtonClick, Vector3.zero);
    }

    public void InitPauseMenu()
    {
        TabViewManager.Instance.DesActiveSettingPanel();
        _currentIndex = 0;
        UpdateSelection();
    }
    private void HandleContinue()
    {
        TabViewManager.Instance.CloseTabView();
    }
    private void HandleSettings()
    {
        GameServices.Get<InputService>().OnUIPauseSettingsOpen();
        TabViewManager.Instance.ActiveSettingPanel();
    }
    private void HandleExit()
    {
        if (GameServices.TryGet<AudioService>(out var audio))
            audio.StopMusicOverride();
        GameServices.Get<ISceneLoader>().LoadScene(SceneNames.Menu, 3.5f);
    }
    private void UpdateSelection()
    {
        for (int i = 0; i < _menuButtons.Count; i++)
        {
            var frame = _menuButtons[i].Q<VisualElement>("Focus_Frame");

            if (frame != null)
                frame.style.opacity = (i == _currentIndex) ? 1 : 0;
        }
    }
    public void OnNavMove(InputAction.CallbackContext ctx)
    {
        if (Time.unscaledTime < _nextNavTime)
            return;

        Vector2 dir = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return;

        if (dir.y > 0.5f)
        {
            MoveFocusVertical(-1);
            _nextNavTime = Time.unscaledTime + _navCooldown;
        }
        else if (dir.y < -0.5f)
        {
            MoveFocusVertical(+1);
            _nextNavTime = Time.unscaledTime + _navCooldown;
        }
    }

    private void MoveFocusVertical(int delta)
    {
        _currentIndex += delta;

        if (_currentIndex < 0)
            _currentIndex = _menuButtons.Count - 1;

        if (_currentIndex >= _menuButtons.Count)
            _currentIndex = 0;

        UpdateSelection();

        if (_audio == null) GameServices.TryGet(out _audio);
        if (_audio != null && _uiSounds != null && !_uiSounds.MenuNavigate.IsNull)
            _audio.PlayOneShot(_uiSounds.MenuNavigate, Vector3.zero);
    }

    public void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;
        if(TabViewManager.Instance.m_TabType==TabType.Menu && TabViewManager.Instance.isActive)
            _buttonActions[_currentIndex]?.Invoke();
    }
}
