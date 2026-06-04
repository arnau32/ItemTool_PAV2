using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuEvents : MonoBehaviour
{
    private UIDocument _document;
    private Button _startButton;
    private Button _settingsButton;
    private Button _creditsButton;
    private Button _exitButton;
    private List<Button> _menuButtons = new List<Button>();


    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;

    private float _navCooldown = 0.5f;
    private float _nextNavTime = 0f;

    private void Awake()
    {
        _input = GameServices.Get<InputService>();

        _document = GetComponent<UIDocument>();

        _startButton = _document.rootVisualElement.Q<Button>("StartButton");
        _settingsButton = _document.rootVisualElement.Q<Button>("SettingsButton");
        _creditsButton = _document.rootVisualElement.Q<Button>("CreditsButton");
        _exitButton = _document.rootVisualElement.Q<Button>("ExitButton");

        _startButton.clicked += HandleStartGame;
        _settingsButton.clicked += HandleSettings;
        _creditsButton.clicked += HandleCredits;
        _exitButton.clicked += HandleExit;

        _menuButtons = _document.rootVisualElement.Query<Button>().ToList();

        foreach (var btn in _menuButtons)
        {
            btn.focusable = true;

            btn.RegisterCallback<PointerEnterEvent>(evt => {
                OnButtonPointerEnter(btn);
            });
            btn.RegisterCallback<PointerLeaveEvent>(evt => {
                OnButtonPointerLeave(btn);
            });
            
            var focusFrame = btn.Q<VisualElement>("Focus_Frame");

            btn.RegisterCallback<FocusInEvent>(_ =>
            {
                if (focusFrame != null)
                    focusFrame.style.opacity = 1;
            });

            btn.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (focusFrame != null)
                    focusFrame.style.opacity = 0;
            });
        }

        var root = _document.rootVisualElement;

        InputActions.UI_MainMenu.Navigate.started += OnNavMove;
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        _startButton?.Focus();
    }

    private void OnDestroy()
    {
        InputActions.UI_MainMenu.Navigate.started -= OnNavMove;
    }

    private void OnButtonPointerEnter(Button btn)
    {
        btn.Focus();
    }

    private void OnButtonPointerLeave(Button btn)
    {
        if (_document.rootVisualElement.focusController.focusedElement == btn)
        {
            var focusedElement = _document.rootVisualElement.focusController.focusedElement;
            _document.rootVisualElement.focusController.focusedElement.Blur();
        }
    }

    private void HandleStartGame() 
    {
        string targetScene = SceneNames.OnBoarding;

        if (GameServices.TryGet<SaveService>(out var save) && save.CurrentSave.meta.onboardingCompleted)
        {
            targetScene = SceneNames.Base;
        }

        SceneManager.LoadScene(targetScene);
    }
    private void HandleSettings()
    {
        MainMenu.Instance.ActiveSettings();
    }

    private void HandleCredits()
    {
        MainMenu.Instance.ActiveCredits();
    }
    private void HandleExit()
    {
        Application.Quit();
    }

    private void OnNavMove(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

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
        var focusedElement = _document.rootVisualElement.focusController.focusedElement;
        Button focusedBtn = focusedElement as Button;

        if (focusedBtn == null)
        {
            _menuButtons[0].Focus();
            return;
        }

        int idx = _menuButtons.IndexOf(focusedBtn);
        if (idx < 0)
        {
            _menuButtons[0].Focus();
            return;
        }

        int next = idx + delta;
        if (next < 0)
            next = _menuButtons.Count - 1;
        if (next >= _menuButtons.Count)
            next = 0;

        _menuButtons[next].Focus();
    }
}
