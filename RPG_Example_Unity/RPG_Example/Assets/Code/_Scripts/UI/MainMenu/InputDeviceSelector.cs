using UnityEngine;

public class InputDeviceSelector : MonoBehaviour
{
    public OptionSelector selector;

    private InputDeviceType[] _devices;
    private InputIconSettings _inputIcons;
    private SettingsService   _settings;

    private void Start()
    {
        _settings   = GameServices.Get<SettingsService>();
        _inputIcons = GameServices.Get<InputIconSettings>();

        _devices = (InputDeviceType[])System.Enum.GetValues(typeof(InputDeviceType));

        string[] options = new string[_devices.Length];
        for (int i = 0; i < _devices.Length; i++)
            options[i] = GetDisplayName(_devices[i]);

        int currentIndex = System.Array.IndexOf(_devices, _inputIcons.CurrentDevice);
        if (currentIndex < 0) currentIndex = 0;

        selector.SetOptions(options, currentIndex);
        selector.OnValueChanged += OnDeviceChanged;

        _inputIcons.OnInputDeviceChanged += SyncFromSystem;
    }

    private void OnDestroy()
    {
        if (_inputIcons != null)
            _inputIcons.OnInputDeviceChanged -= SyncFromSystem;
    }

    private void OnDeviceChanged(int index)
    {
        _settings.InputDevice = _devices[index];
    }

    private void SyncFromSystem()
    {
        int index = System.Array.IndexOf(_devices, _inputIcons.CurrentDevice);
        if (index < 0) index = 0;
        selector.SetIndexWithoutNotify(index);
    }

    private string GetDisplayName(InputDeviceType device)
    {
        return device switch
        {
            InputDeviceType.Xbox        => "Xbox",
            InputDeviceType.PlayStation => "PlayStation",
            _                           => device.ToString()
        };
    }
}