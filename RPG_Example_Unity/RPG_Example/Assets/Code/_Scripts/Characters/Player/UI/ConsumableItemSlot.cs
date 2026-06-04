using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConsumableItemSlot : MonoBehaviour
{
    public Image logo;
    public TextMeshProUGUI number;
    public int SlotNumber;

    public void UpdateUI(ItemStack stack)
    {
        if (stack == null || stack.data == null)
        {
            logo.enabled = false;
            number.text = "";
            return;
        }

        logo.enabled = true;
        logo.sprite = stack.data.icon;

        number.text = stack.quantity > 1
            ? stack.quantity.ToString()
            : "";
    }
}
