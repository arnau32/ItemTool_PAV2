using UnityEngine;
using UnityEngine.UIElements;

public class TabViewInputIcon : MonoBehaviour
{
    private IconDatabase iconDatabase;
    private InputIconSettings _inputIcons;

    public static TabViewInputIcon Instance;
    private VisualElement _inputIconSwitchTabLeft;
    private VisualElement _inputIconSwitchTabRight;

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


    public void Init()
    {
        _inputIcons = GameServices.Get<InputIconSettings>();

        _inputIconSwitchTabLeft = UIManager.Instance.root.Q<VisualElement>("LT_Image");
        _inputIconSwitchTabRight = UIManager.Instance.root.Q<VisualElement>("RT_Image");

        _inputIcons.OnInputDeviceChanged += Refresh;
        Refresh();
    }


    public void Refresh()
    {
        iconDatabase = _inputIcons.CurrentIconDatabase;

        var bg = new StyleBackground(iconDatabase.Get(InputIconType.LT));
        _inputIconSwitchTabLeft.style.backgroundImage = bg;

        bg = new StyleBackground(iconDatabase.Get(InputIconType.RT));
        _inputIconSwitchTabRight.style.backgroundImage = bg;

    }
}
