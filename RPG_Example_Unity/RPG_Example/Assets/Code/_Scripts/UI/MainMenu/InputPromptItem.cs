using UnityEngine;
using TMPro;

public class InputPromptItem : MonoBehaviour
{
    public InputIcon icon;
    public TextMeshProUGUI label;

    public void Setup(InputIconType type, string text)
    {
        icon.iconType = type;
        label.text = text;

        icon.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
    }
}