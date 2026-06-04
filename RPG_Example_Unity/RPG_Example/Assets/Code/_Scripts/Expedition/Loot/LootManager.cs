using System.Collections.Generic;
using UnityEngine;

public class LootManager : MonoBehaviour, IGameServices
{
    [Header("Prefabs")] [Tooltip("Prefab de bolsa/cofre que lleva un componente Storage.")] [SerializeField]
    private Storage bagPrefab;

    private void Awake()
    {
        if (bagPrefab == null)
        {
            Debug.LogWarning("LootManager: bagPrefab no asignado en el inspector.");
        }
    }

    /// <summary>
    /// Genera loot a partir de una LootTable y spawnea el loot en un container (Storage)
    /// en la posicion indicada, con esos items dentro.
    /// </summary>
    public void SpawnLoot(LootTable table, Transform _transform, Storage prefab)
    {
        if (table == null)
        {
            Debug.LogWarning("LootManager.SpawnLoot llamado con LootTable nula.");
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning("LootManager: prefab es nulo, no se puede spawnear bolsa de loot.");
            return;
        }

        List<ItemStack> lootStacks = LootSystem.Roll(table);
        if (lootStacks == null || lootStacks.Count == 0)
        {
            // Sin drops ? no spawneamos nada
            return;
        }

        Storage storageInstance = Instantiate(prefab, _transform.position, _transform.rotation);
        storageInstance.transform.localScale = _transform.lossyScale;
        storageInstance.AddItems(lootStacks);

        for (int i = 0; i < lootStacks.Count; i++)
        {
            var s = lootStacks[i];
            if (s?.data == null) continue;
            string id = !string.IsNullOrEmpty(s.data.uniqueID) ? s.data.uniqueID : s.data.itemNameID;
            CombatAnalytics.LootSpawned(id, s.data.itemType.ToString(), s.data.value, s.quantity, _transform.position);
        }
        
    }

    public void SpawnLootBag(LootTable table, Vector3 position)
    {
        if (table == null)
        {
            Debug.LogWarning("LootManager.SpawnLoot llamado con LootTable nula.");
            return;
        }

        if (bagPrefab == null)
        {
            Debug.LogWarning("LootManager: prefab es nulo, no se puede spawnear bolsa de loot.");
            return;
        }

        List<ItemStack> lootStacks = LootSystem.Roll(table);
        if (lootStacks == null || lootStacks.Count == 0)
        {
            // Sin drops ? no spawneamos nada
            return;
        }

        Storage storageInstance = Instantiate(bagPrefab, position, Quaternion.identity);
        storageInstance.AddItems(lootStacks);

        for (int i = 0; i < lootStacks.Count; i++)
        {
            var s = lootStacks[i];
            if (s?.data == null) continue;
            string id = !string.IsNullOrEmpty(s.data.uniqueID) ? s.data.uniqueID : s.data.itemNameID;
            CombatAnalytics.LootSpawned(id, s.data.itemType.ToString(), s.data.value, s.quantity, position);
        }

#if UNITY_EDITOR
        Debug.Log($"[LootManager] Spawned loot bag with {lootStacks.Count} stacks at {position}.");
#endif
    }

    public void SpawnLootFromStacks(List<ItemStack> stacks, Vector3 pos)
    {
        Storage s = Instantiate(bagPrefab, pos, Quaternion.identity);
        s.AddItems(stacks);
    }
}