using UnityEngine;

public class HUDVisibilityController : MonoBehaviour
{
    [SerializeField] private GameObject _hudToHide;

    private bool _tabViewOpen;
    private bool _dialogueOpen;
    private bool _skillTreeOpen;
    private bool _noteOpen;
    private bool _mapOpen;

    #region Unity Lifecycle

    private void Start()
    {
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        
        if (TabViewManager.Instance != null)
        {
            TabViewManager.Instance.TabOpen += OnTabViewOpened;
            TabViewManager.Instance.OnTabClose += OnTabViewClosed;
        }
        else
        {
            Debug.LogWarning("[HUDVisibilityController] TabViewManager.Instance is null on Start — HUD will not respond to inventory/pause events.");
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStart += OnDialogueStarted;
            DialogueManager.Instance.OnDialogueEnd += OnDialogueEnded;
        }
        else
        {
            Debug.LogWarning("[HUDVisibilityController] DialogueManager.Instance is null on Start — HUD will not respond to dialogue events.");
        }

        SkillTreeUI.OnOpened += OnSkillTreeOpened;
        SkillTreeUI.OnClosed += OnSkillTreeClosed;

        NoteUI.OnOpened += OnNoteOpened;
        NoteUI.OnClosed += OnNoteClosed;
        
        GameServices.Get<InputService>().OnMapOpen += OnOnMapOpen;
        
    }

    private void OnOnMapOpen(bool mapOpen)
    {
        if (mapOpen)
        {
            OnMapOpened();
        }
        else
        {
            OnMapClosed();
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (TabViewManager.Instance != null)
        {
            TabViewManager.Instance.TabOpen -= OnTabViewOpened;
            TabViewManager.Instance.OnTabClose -= OnTabViewClosed;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStart -= OnDialogueStarted;
            DialogueManager.Instance.OnDialogueEnd -= OnDialogueEnded;
        }

        SkillTreeUI.OnOpened -= OnSkillTreeOpened;
        SkillTreeUI.OnClosed -= OnSkillTreeClosed;

        NoteUI.OnOpened -= OnNoteOpened;
        NoteUI.OnClosed -= OnNoteClosed;
        GameServices.Get<InputService>().OnMapOpen -= OnOnMapOpen;
    }

    #endregion

    #region Handlers

    private void OnTabViewOpened(TabType _)
    {
        _tabViewOpen = true;
        RefreshHUD();
    }

    private void OnTabViewClosed()
    {
        _tabViewOpen = false;
        RefreshHUD();
    }

    private void OnDialogueStarted()
    {
        _dialogueOpen = true;
        RefreshHUD();
    }

    private void OnDialogueEnded()
    {
        _dialogueOpen = false;
        RefreshHUD();
    }

    private void OnSkillTreeOpened()
    {
        _skillTreeOpen = true;
        RefreshHUD();
    }

    private void OnSkillTreeClosed()
    {
        _skillTreeOpen = false;
        RefreshHUD();
    }

    private void OnNoteOpened()
    {
        _noteOpen = true;
        RefreshHUD();
    }

    private void OnNoteClosed()
    {
        _noteOpen = false;
        RefreshHUD();
    }
    
    private void OnMapOpened()
    {
        _mapOpen = true;
        RefreshHUD();
    }

    private void OnMapClosed()
    {
        _mapOpen = false;
        RefreshHUD();
    }

    #endregion

    #region HUD Control

    private void RefreshHUD()
    {
        if (_hudToHide == null) return;

        bool shouldHide = _tabViewOpen || _dialogueOpen || _skillTreeOpen || _noteOpen || _mapOpen;
        _hudToHide.SetActive(!shouldHide);
    }

    #endregion
}