using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class SettingsTabs : MonoBehaviour
{
    public GameObject[] panels;

    private int currentIndex = 0;

    public TabVisual[] tabVisuals;

    public bool isActive = false;

    private InputService _input;
    private PlayerInputActions InputActions => _input.Actions;

    private PanelEventHandler[] _panelEventHandlers;

    private void Awake()
    {
        _input = GameServices.Get<InputService>();
    }
    void Start()
    {
        ShowTab(0);
        InputActions.UI_Settings.NextTab.started += NextTab;
        InputActions.UI_Settings.PrevTab.started += PrevTab;
    }

    private void OnEnable()
    {
        if (!isActive) return;
        ShowTab(0);
        SetPanelEventHandlersEnabled(false);
    }

    private void OnDisable()
    {
        SetPanelEventHandlersEnabled(true);
    }

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
    public void ShowTab(int index)
    {
        currentIndex = index;

        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].SetActive(i == index);

            tabVisuals[i].SetSelected(i == index);
        }
    }

    public void NextTab(InputAction.CallbackContext ctx)
    {
        int next = (currentIndex + 1) % panels.Length;
        ShowTab(next);
    }

    public void PrevTab(InputAction.CallbackContext ctx)
    {
        int prev = (currentIndex - 1 + panels.Length) % panels.Length;
        ShowTab(prev);
    }

}