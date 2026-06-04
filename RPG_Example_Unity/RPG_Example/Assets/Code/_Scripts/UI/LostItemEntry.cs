using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LostItemEntry : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _quantity;

    public void SetData(ItemStack stack, int overrideQuantity = -1)
    {
        _icon.sprite = stack.data.icon;
        int qty = overrideQuantity >= 0 ? overrideQuantity : stack.quantity;
        _quantity.text = Mathf.Max(qty, 1).ToString();
    }
}
