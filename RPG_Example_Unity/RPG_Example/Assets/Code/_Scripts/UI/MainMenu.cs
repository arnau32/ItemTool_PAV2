using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    private UIDocument _document;
    private VisualElement _MainMenuPanel;
    private VisualElement _CreditsPanel;
    private VisualElement _SettingsPanel;

    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;

    public static MainMenu Instance;

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
        _document = GetComponent<UIDocument>();
        _MainMenuPanel = _document.rootVisualElement.Q<VisualElement>("MainMenuPanel");
        _CreditsPanel = _document.rootVisualElement.Q<VisualElement>("CreditsPanel");
        _SettingsPanel = _document.rootVisualElement.Q<VisualElement>("SettingsPanel");


        InputActions.UI_Credits.DesactiveCredits.performed += DesActiveCredits;

        _MainMenuPanel.style.display = DisplayStyle.Flex;
        _CreditsPanel.style.display = DisplayStyle.None;
        _SettingsPanel.style.display = DisplayStyle.None;
    }

    public void ActiveCredits()
    {
        GameServices.Get<InputService>().OnUICreditsOpen();
        _MainMenuPanel.style.display = DisplayStyle.None;
        _CreditsPanel.style.display = DisplayStyle.Flex;
    }
    public void DesActiveCredits(InputAction.CallbackContext ctx)
    {
        GameServices.Get<InputService>().OnUICreditsClose();
        _MainMenuPanel.style.display = DisplayStyle.Flex;
        _CreditsPanel.style.display = DisplayStyle.None;
    }

    public void ActiveSettings()
    {
        GameServices.Get<InputService>().OnUISettingsOpen();
        _MainMenuPanel.style.display = DisplayStyle.None;
        _SettingsPanel.style.display = DisplayStyle.Flex;
    }
    public void DesActiveSettings(InputAction.CallbackContext ctx)
    {
        GameServices.Get<InputService>().OnUISettingsClose();
        _MainMenuPanel.style.display = DisplayStyle.Flex;
        _SettingsPanel.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        if (_input != null)
            InputActions.UI_Credits.DesactiveCredits.performed -= DesActiveCredits;
        Instance = null;
    }
}
