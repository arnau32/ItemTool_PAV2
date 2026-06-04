using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    #region Fields

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    [Tooltip("Panel with Normal / Easy buttons shown when starting a New Game.")]
    public GameObject difficultyPanel;

    public Button _continueButton;

    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _input = GameServices.Get<InputService>();
    }

    private void Start()
    {
        GameServices.Get<InputService>().OnUIMainMenuOpen();

        InputActions.UI_Credits.DesactiveCredits.started   += CloseCredits;
        InputActions.UI_Settings.DesactiveSettings.started += CloseSettings;
        InputActions.UI_Settings.DesactiveSettings.started += OnCancelDifficulty;

        Cursor.lockState = CursorLockMode.Locked;

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        bool hasSave        = GameServices.TryGet<SaveService>(out var save);
        bool cinematicDone  = hasSave && save.CurrentSave.meta.cinematicCompleted;
        bool onboardingDone = hasSave && save.CurrentSave.meta.onboardingCompleted;

        _continueButton.interactable = cinematicDone || onboardingDone;
    }

    #endregion

    #region OnDestroy

    private void OnDestroy()
    {
        InputActions.UI_Credits.DesactiveCredits.started   -= CloseCredits;
        InputActions.UI_Settings.DesactiveSettings.started -= CloseSettings;
        InputActions.UI_Settings.DesactiveSettings.started -= OnCancelDifficulty;
        GameServices.Get<InputService>()?.OnUIMainMenuClose();
    }

    #endregion

    #region Public API

    // Called by the "New Game" button — shows the difficulty selector instead of
    // starting directly so the player can choose Normal or Easy first.
    public void ShowDifficultySelector()
    {
        if (difficultyPanel == null)
        {
            StartNewGame(0);
            return;
        }

        mainPanel.SetActive(false);
        difficultyPanel.SetActive(true);
    }

    // Called by the "Normal" button in the difficulty panel.
    public void SelectNormal() => StartNewGame(0);

    // Called by the "Easy" button in the difficulty panel.
    public void SelectEasy() => StartNewGame(1);

    // Called by the "Back" button in the difficulty panel.
    public void BackFromDifficulty()
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        mainPanel.SetActive(true);
    }

    public void ContinueGame()
    {
        var save = GameServices.Get<SaveService>();

        if (save.CurrentSave.meta.onboardingCompleted)
        {
            GameServices.Get<ISceneLoader>().LoadScene(save.CurrentSave.meta.lastScene);
            return;
        }

        // Cinematic seen but onboarding not done — restart onboarding from scratch.
        save.WipeSave();
        save.CurrentSave.meta.cinematicCompleted = true;
        save.Save();
        GameServices.Get<ISceneLoader>().LoadScene(SceneNames.OnBoarding);
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        GameServices.Get<InputService>().OnUISettingsOpen();
    }

    public void CloseSettings(InputAction.CallbackContext ctx)
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
        GameServices.Get<InputService>().OnUISettingsClose();
    }

    public void OpenCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
        GameServices.Get<InputService>().OnUICreditsOpen();
    }

    public void CloseCredits(InputAction.CallbackContext ctx)
    {
        creditsPanel.SetActive(false);
        mainPanel.SetActive(true);
        GameServices.Get<InputService>().OnUICreditsClose();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Private

    private void StartNewGame(int difficulty)
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        if (GameServices.TryGet<SaveService>(out var save))
        {
            save.WipeSave();
            save.SetDifficulty(difficulty);
        }

        GameServices.Get<AudioService>().StopBackgroundAudioFaded(1.5f);
        GameServices.Get<ISceneLoader>().LoadScene(SceneNames.Cinematic, 3.5f, 0);
    }

    // Closes the difficulty panel with the cancel/back input action (same as settings/credits).
    private void OnCancelDifficulty(InputAction.CallbackContext ctx)
    {
        if (difficultyPanel == null || !difficultyPanel.activeSelf) return;
        BackFromDifficulty();
    }

    #endregion
}