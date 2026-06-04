using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class ConsumableItemSlotManager : MonoBehaviour
{
    public List<ConsumableItemSlot> slots = new();

    public static ConsumableItemSlotManager Instance;

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

    private async void Start()
    {
        await UniTask.WaitUntil(() => PlayerInventory.Instance.InventoryInit);
        RefreshAllSlots();
    }
    public void RefreshAllSlots()
    {
        foreach (var slotUI in slots)
        {
            var container = ContainerRegistry.GetConsumableSlot(slotUI.SlotNumber);

            if (container == null)
            {
                slotUI.UpdateUI(null);
                continue;
            }

            slotUI.UpdateUI(container.currentStack);
        }
    }

    public void RefreshSlot(int slotNumber)
    {
        var slotUI = slots.Find(s => s.SlotNumber == slotNumber);
        if (slotUI == null) return;

        var container = ContainerRegistry.GetConsumableSlot(slotNumber);
        slotUI.UpdateUI(container?.currentStack);
    }
}
