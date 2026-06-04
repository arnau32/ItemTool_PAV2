using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InputDeviceType
{
    Xbox,
    PlayStation,
}

public class InputIconSettings : MonoBehaviour, IGameServices
{
    [SerializeField] public IconDatabase xboxIcons;
    [SerializeField] public IconDatabase playstationIcons;

    public InputDeviceType CurrentDevice      { get; private set; }
    public IconDatabase    CurrentIconDatabase { get; private set; }

    public event Action OnInputDeviceChanged;

    private bool    _isManuallyOverridden;
    private Gamepad _lastGamepad;

    private void Awake()
    {
        CurrentDevice        = (InputDeviceType)(-1);
        CurrentIconDatabase  = xboxIcons;
    }

    private void Update()
    {
        DetectInputDevice();
    }

    #region Public API

    public void SetDevice(InputDeviceType device)
    {
        SetDevice(device, true);
    }

    #endregion

    #region Private

    private void DetectInputDevice()
    {
        var gamepad = Gamepad.current;

        if (gamepad == null)
        {
            _isManuallyOverridden = false;
            _lastGamepad          = null;
            return;
        }

        if (gamepad != _lastGamepad)
        {
            _lastGamepad = gamepad;

            if (!_isManuallyOverridden)
                AutoDetectDevice(gamepad);
        }
    }

    private void AutoDetectDevice(Gamepad gamepad)
    {
        string name = gamepad.name;

        if (name.Contains("DualShock") || name.Contains("DualSense") || name.Contains("PlayStation"))
            SetDevice(InputDeviceType.PlayStation, false);
        else
            SetDevice(InputDeviceType.Xbox, false);
    }

    private void SetDevice(InputDeviceType device, bool manual)
    {
        if (CurrentDevice == device) return;

        CurrentDevice = device;

        switch (device)
        {
            case InputDeviceType.Xbox:
                CurrentIconDatabase = xboxIcons;
                break;

            case InputDeviceType.PlayStation:
                CurrentIconDatabase = playstationIcons;
                break;
        }

        if (manual)
            _isManuallyOverridden = true;

        OnInputDeviceChanged?.Invoke();
    }

    #endregion
}