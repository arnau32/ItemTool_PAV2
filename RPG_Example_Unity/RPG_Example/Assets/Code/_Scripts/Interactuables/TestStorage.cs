using System.Collections.Generic;
using UnityEngine;

public class TestStorage : BaseInteractable
{
    [Header("Test Items")]
    public List<ItemStack> items = new();

    [Header("UI")]
    public StorageType storageType = StorageType.Chest;

    public override void OnInteract()
    {
        base.OnInteract();

        if (items == null)
            items = new List<ItemStack>();

        TabViewManager.Instance.OpenLoots(items, storageType);
    }
}