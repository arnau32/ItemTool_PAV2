using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.UIElements;

public class DialogueManager : MonoBehaviour
{
    private IconDatabase      iconDatabase;
    private InputIconSettings _inputIcons;
    private AudioService      _audio;

    [SerializeField] private UISounds _uiSounds;

    public static DialogueManager Instance;
    
    public event System.Action OnDialogueStart;
    public float textSpeed;
    public event System.Action OnDialogueEnd;

    #region UI References

    private static Label _dialogueText;
    private VisualElement _choiceContainer;
    private VisualElement _characterIllustration;
    private Label _characterName;
    private VisualElement _root;
    private VisualElement _inputIcon;

    #endregion

    #region State

    private DialogueGraph _graph;
    private DialogueNode _currentNode;

    private List<Button> _activeButtons = new();

    // Parallel list to _activeButtons: only options that passed EvaluateConditions().
    // _currentIndex always indexes into this list, never into node.options directly,
    // so gamepad navigation stays in sync when hidden options leave gaps.
    private readonly List<DialogueOption> _visibleOptions = new();

    private int _currentIndex;
    private bool   _choiceJustOpened;
    private bool   _isActive;
    private bool   _isLoadingText;
    private string _currentDisplayedText;

    // When false, EndDialogue skips OnUIClose — the caller manages the input map.
    // Used by Cinematic_OnBoarding to keep input locked across multiple dialogues.
    private bool _manageInput = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    async void Start()
    {
        _inputIcons = GameServices.Get<InputIconSettings>();
        GameServices.TryGet(out _audio);
        Configure();
        await UniTask.WaitUntil(() => UIManager.Instance.AllUIInitFinish);

        _inputIcons.OnInputDeviceChanged += Refresh;
        Refresh();
    }

    #endregion

    #region Public API

    public bool IsActive => _isActive;

    /// <summary>
    /// Starts a dialogue. Input map is switched to UI on start and back to Player on end.
    /// </summary>
    public void StartDialogue(DialogueGraph g) => StartDialogue(g, manageInput: true);

    /// <summary>
    /// Starts a dialogue with explicit input management control.
    /// Pass manageInput:false when the caller (e.g. Cinematic_OnBoarding) owns the
    /// input map for the full sequence — EndDialogue will not call OnUIClose.
    /// </summary>
    public void StartDialogue(DialogueGraph g, bool manageInput)
    {
        if (g == null)
        {
            Debug.LogWarning("[Dialogue Manager] StartDialogue called with null graph.");
            return;
        }

        _manageInput = manageInput;

        _characterIllustration.style.backgroundImage = g.characterIllustration.texture;
        _characterName.text = g.characterName;

        _graph = g;
        _isActive = true;
        OnDialogueStart?.Invoke();
        PlayerInventory.Instance.CanOpenInventory = false;
        _dialogueText.text = "";
        UIManager.Instance.dialoguePanel.style.display = DisplayStyle.Flex;

        GoToNode(_graph.startNodeId);
    }

    /// <summary>
    /// Starts a dialogue beginning at a specific node instead of the graph's default start.
    /// Useful for mid-graph entry points such as the revival sequence.
    /// </summary>
    public void StartDialogueFromNode(DialogueGraph g, string nodeId)
    {
        if (g == null)
        {
            Debug.LogWarning("[Dialogue Manager] StartDialogueFromNode called with null graph.");
            return;
        }

        _manageInput = true;

        _characterIllustration.style.backgroundImage = g.characterIllustration.texture;
        _characterName.text = g.characterName;

        _graph    = g;
        _isActive = true;
        OnDialogueStart?.Invoke();
        PlayerInventory.Instance.CanOpenInventory = false;
        _dialogueText.text = "";
        UIManager.Instance.dialoguePanel.style.display = DisplayStyle.Flex;

        GoToNode(nodeId);
    }

    #endregion

    #region Input Callbacks

    public void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !_isActive || _currentNode == null) return;

        switch (_currentNode.nodeType)
        {
            case DialogueNodeType.Text:
            case DialogueNodeType.Random:
                HandleTextSubmit();
                break;

            case DialogueNodeType.Choice:
                HandleChoiceSubmit();
                break;
        }
    }

    private float _moveRepeatDelay = 0.25f; // 首次延迟
    private float _moveRepeatRate = 0.15f; // 连续移动速度

    private float _nextMoveTime = 0f;
    private int _lastMoveDir = 0; // -1 上，1 下，0 无

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 move = ctx.ReadValue<Vector2>();

        if (_activeButtons.Count == 0) return;

        int dir = 0;

        if (move.y > 0.5f) dir = -1;     // 上
        else if (move.y < -0.5f) dir = 1; // 下

        if (dir == 0)
        {
            _lastMoveDir = 0;
            return;
        }

        // 👉 第一次按下
        if (_lastMoveDir == 0)
        {
            MoveSelection(dir);

            _lastMoveDir = dir;
            _nextMoveTime = Time.time + _moveRepeatDelay;
        }
        // 👉 长按
        else if (Time.time >= _nextMoveTime)
        {
            MoveSelection(dir);
            _nextMoveTime = Time.time + _moveRepeatRate;
        }
    }

    private void MoveSelection(int dir)
    {
        _currentIndex += dir;
        _currentIndex = Mathf.Clamp(_currentIndex, 0, _activeButtons.Count - 1);

        UpdateButtonFocus();
    }

    public void OnCancel(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !_isActive || _currentNode == null) return;
        if (_currentNode.nodeType != DialogueNodeType.Choice) return;

        EndDialogue();
    }

    #endregion

    #region Private Methods

    private void GoToNode(string id)
    {
        // Empty id = dialogue finished
        if (string.IsNullOrEmpty(id))
        {
            EndDialogue();
            return;
        }

        var node = _graph.GetNode(id);

        if (node == null)
        {
            Debug.LogWarning($"[DialogueManager] Node '{id}' not found in graph '{_graph.name}'. Ending dialogue.");
            EndDialogue();
            return;
        }

        _currentNode = node;

        // onEnterActions fire for Text, Random and End — Choice actions fire per-option on selection.
        if (node.nodeType != DialogueNodeType.Choice)
            FireActions(node.onEnterActions);

        switch (_currentNode.nodeType)
        {
            case DialogueNodeType.Text:
                ShowTextNode();
                break;

            case DialogueNodeType.Random:
                ShowRandomNode();
                break;

            case DialogueNodeType.Choice:
                ShowChoiceNode(node);
                break;

            case DialogueNodeType.End:
                EndDialogue();
                break;
        }
    }

    public void Refresh()
    {
        iconDatabase = _inputIcons.CurrentIconDatabase;

        var bg = new StyleBackground(iconDatabase.Get(InputIconType.ButtonSouth));
        _inputIcon.style.backgroundImage = bg;
    }

    private async void ShowTextNode()
    {
        _dialogueText.style.display = DisplayStyle.Flex;

        _currentDisplayedText = await GetLocalizedText(_currentNode.localizedText);

        _dialogueText.text = "";
        StartCoroutine(TypeLine(_currentDisplayedText));
    }

    private async void ShowRandomNode()
    {
        var texts = _currentNode.randomTexts;

        if (texts == null || texts.Count == 0)
        {
            Debug.LogWarning($"[DialogueManager] Random node '{_currentNode.id}' has no texts. Skipping.");
            GoToNode(_currentNode.nextNodeId);
            return;
        }

        _dialogueText.style.display = DisplayStyle.Flex;

        var chosen = texts[Random.Range(0, texts.Count)];
        _currentDisplayedText = await GetLocalizedText(chosen);

        _dialogueText.text = "";
        StartCoroutine(TypeLine(_currentDisplayedText));
    }

    private async void ShowChoiceNode(DialogueNode node)
    {
        // ── Router mode ───────────────────────────────────────────────────────
        // isRouter = true: evaluate conditions top-to-bottom and jump directly
        // to the first passing option with no UI. First match wins.
        if (node.isRouter)
        {
            for (int i = 0; i < node.options.Count; i++)
            {
                var opt = node.options[i];
                if (opt == null || !opt.EvaluateConditions()) continue;

                FireActions(opt.actions);
                GoToNode(opt.nextNodeId);
                return;
            }

            Debug.LogWarning($"[DialogueManager] Router node '{node.id}' had no passing option. Ending dialogue.");
            EndDialogue();
            return;
        }

        // ── Normal choice mode ────────────────────────────────────────────────
        _dialogueText.style.display = DisplayStyle.None;

        ClearButtons();
        _visibleOptions.Clear();

        _currentIndex = 0;

        foreach (var opt in node.options)
        {
            if (opt == null) continue;

            // Skip options whose conditions are not met.
            // EvaluateConditions() returns true when the list is empty (always visible).
            if (!opt.EvaluateConditions()) continue;

            var capturedOpt = opt;
            _visibleOptions.Add(opt);

            string label = await GetLocalizedText(opt.localizedOptionText);

            var btn = new Button
            {
                focusable = true,
                text      = label
            };

            btn.AddToClassList("choice-button");
            btn.clicked += () => OnSelectOption(capturedOpt);

            _choiceContainer.Add(btn);
            _activeButtons.Add(btn);
        }

        if (_activeButtons.Count == 0)
        {
            Debug.LogWarning("[DialogueManager] All options hidden by conditions. Ending dialogue.");
            EndDialogue();
            return;
        }

        UpdateButtonFocus();

        _choiceJustOpened = true;
        StartCoroutine(ResetChoiceOpenFlag());
    }

    private void EndDialogue()
    {
        _isActive = false;
        _graph = null;
        _currentNode = null;
        _currentIndex = 0;

        ClearButtons();
        _visibleOptions.Clear();

        UIManager.Instance.dialoguePanel.style.display = DisplayStyle.None;
        PlayerInventory.Instance.CanOpenInventory = true;

        // Only restore input if this dialogue owns the input map.
        // When manageInput:false the caller (e.g. Cinematic_OnBoarding) controls it.
        if (_manageInput)
            GameServices.Get<InputService>().OnUIClose();

        OnDialogueEnd?.Invoke();
    }

    private void HandleTextSubmit()
    {
        if (_dialogueText.text == _currentDisplayedText)
        {
            // Text fully displayed -> advance.
            PlayUISound(_uiSounds?.DialoguePageTurn ?? default);
            GoToNode(_currentNode.nextNodeId);
        }
        else
        {
            // Skip typewriter -> show full text immediately.
            StopAllCoroutines();
            _dialogueText.text = _currentDisplayedText;
        }
    }

    private void HandleChoiceSubmit()
    {
        if (_choiceJustOpened) return;
        if (_activeButtons.Count == 0) return;
        if (_currentIndex >= _visibleOptions.Count) return;

        // Index into _visibleOptions — always in sync with _activeButtons.
        OnSelectOption(_visibleOptions[_currentIndex]);
    }

    private void OnSelectOption(DialogueOption opt)
    {
        // Fire option-specific actions BEFORE navigating.
        FireActions(opt.actions);

        ClearButtons();
        _visibleOptions.Clear();

        _dialogueText.style.display = DisplayStyle.Flex;
        _dialogueText.text = "";

        GoToNode(opt.nextNodeId);
    }

    private void ClearButtons()
    {
        foreach (var btn in _activeButtons)
            btn.RemoveFromHierarchy();

        _activeButtons.Clear();
    }

    private void FireActions(List<DialogueAction> actions)
    {
        if (actions == null || actions.Count == 0) return;

        foreach (DialogueAction action in actions)
        {
            if (action != null) action.Execute();
        }
    }

    private void Configure()
    {
        iconDatabase = _inputIcons.CurrentIconDatabase;

        _root = UIManager.Instance.dialoguePanel;
        _root.focusable = true;

        VisualElement dialogueBox = _root.Q<VisualElement>("DialogueBox");
        _dialogueText = dialogueBox.Q<Label>("DialogueText");
        _choiceContainer = dialogueBox.Q<VisualElement>("ChoiceContainer");
        _characterIllustration = dialogueBox.Q<VisualElement>("characterIllustration");
        _characterName = _root.Q<Label>("Name");
        _inputIcon = dialogueBox.Q<VisualElement>("InputIcon");
    }

    public async Task<string> GetLocalizedText(LocalizedString ls) => await ls.GetLocalizedStringAsync();

    private IEnumerator TypeLine(string line)
    {
        bool hasLetterSound = _uiSounds != null && !_uiSounds.DialogueLetter.IsNull;

        foreach (var c in line)
        {
            _dialogueText.text += c;
            if (hasLetterSound && !char.IsWhiteSpace(c))
                PlayUISound(_uiSounds.DialogueLetter);
            yield return new WaitForSeconds(textSpeed);
        }
    }

    private void PlayUISound(FMODUnity.EventReference sound)
    {
        if (_audio == null)
            GameServices.TryGet(out _audio);

        if (_audio == null || sound.IsNull) return;
        _audio.PlayOneShot(sound, Vector3.zero);
    }

    private void UpdateButtonFocus()
    {
        for (int i = 0; i < _activeButtons.Count; i++)
        {
            var btn = _activeButtons[i];

            if (i == _currentIndex)
                btn.AddToClassList("choice-button-focus");
            else
                btn.RemoveFromClassList("choice-button-focus");
        }
    }

    private IEnumerator ResetChoiceOpenFlag()
    {
        yield return null;
        _choiceJustOpened = false;
    }

    #endregion
}