using System.Collections.Generic;
using UnityEngine;
using Gameplay.Enemies;

public static class RunInitializationSystem
{
    public static void InitializeRun(
        ExpeditionManager expeditionManager,
        ExpeditionDefinition def,
        int seed)
    {
        if (expeditionManager == null || def == null)
        {
            Debug.LogError("RunInitializationSystem: parámetros nulos.");
            return;
        }

        Random.InitState(seed);

        if (!GameServices.TryGet<EnemyManager>(out var enemyManager))
        {
            Debug.LogError("RunInitializationSystem: EnemyManager not registered in GameServices.");
            return;
        }

        GameServices.TryGet<LootManager>(out var lootManager);

        InitializeEnemies(def, enemyManager);
        InitializeLoot(def, lootManager);
    }

    #region Enemies

    private static void InitializeEnemies(ExpeditionDefinition def, EnemyManager enemyManager)
    {
        var allEnemyPoints = GameObject.FindObjectsByType<EnemyPoint>(FindObjectsSortMode.None);
        var enemyPoints = new List<EnemyPoint>();

        foreach (var p in allEnemyPoints)
        {
            var candidates = GetEnemyCandidatesForPoint(p, def);
            if (candidates != null && candidates.Count > 0)
                enemyPoints.Add(p);
        }

        if (enemyPoints.Count == 0)
        {
            Debug.LogWarning("RunInit.Enemies: No hay EnemyPoints con candidatos válidos.");
            return;
        }

        var forcedPoints = new List<EnemyPoint>();
        var optionalPoints = new List<EnemyPoint>();

        foreach (var p in enemyPoints)
        {
            if (p.forceSpawn) forcedPoints.Add(p);
            else optionalPoints.Add(p);
        }

        int optionalCapacity = 0;
        foreach (var p in optionalPoints)
        {
            optionalCapacity += p.allowMultipleEnemies ? Mathf.Max(1, p.maxEnemies) : 1;
        }

        int minRandom = Mathf.Max(0, def.minEnemyCount);
        int maxRandom = Mathf.Max(minRandom, def.maxEnemyCount);

        if (optionalCapacity == 0)
        {
            if (minRandom > 0)
            {
                Debug.LogWarning(
                    $"RunInit.Enemies: Capacidad opcional = 0 pero minEnemyCount = {minRandom}. " +
                    "Solo se spawnearán los puntos forzados.");
            }

            minRandom = 0;
            maxRandom = 0;
        }
        else
        {
            if (optionalCapacity < minRandom)
            {
                Debug.LogWarning(
                    $"RunInit.Enemies: Capacidad opcional ({optionalCapacity}) < minEnemyCount ({minRandom}). " +
                    "Ajustando min/max al máximo opcional posible.");

                minRandom = optionalCapacity;
                maxRandom = optionalCapacity;
            }
            else
            {
                maxRandom = Mathf.Min(maxRandom, optionalCapacity);
            }
        }

        int targetRandomCount = Random.Range(minRandom, maxRandom + 1);
        int forcedCount = 0;
        int randomCount = 0;
        int maxForcedCount = 0;

        foreach (var p in forcedPoints)
        {
            forcedCount += SpawnFromPointForced(p, def, enemyManager);
            maxForcedCount += p.allowMultipleEnemies ? Mathf.Max(1, p.maxEnemies) : 1;
        }

        var optionalAvailable = new List<EnemyPoint>(optionalPoints);

        while (randomCount < targetRandomCount && optionalAvailable.Count > 0)
        {
            int index = Random.Range(0, optionalAvailable.Count);
            var p = optionalAvailable[index];
            optionalAvailable.RemoveAt(index);

            int remaining = targetRandomCount - randomCount;
            randomCount += SpawnFromPointRandom(p, def, remaining, enemyManager);
        }

        int total = forcedCount + randomCount;
        int maxTotal = maxForcedCount + maxRandom;

        Debug.Log(
            "<b>[Enemy Initialization Stats]</b>\n" +
            $"<b>Expedition needed:</b> {def.minEnemyCount} to {def.maxEnemyCount}\n" +
            $"<b>Forced:</b> {forcedCount} / {maxForcedCount}\n" +
            $"<b>Random:</b> {randomCount} / {maxRandom}\n" +
            $"<b>Total:</b> {total} / {maxTotal}\n\n" +
            $"<b>Random Target:</b> {targetRandomCount}\n" +
            $"<b>Random Range:</b> {minRandom} - {maxRandom}\n" +
            $"<b>Optional Capacity:</b> {optionalCapacity}\n\n" +
            $"<b>Points Used:</b>\n" +
            $"    Forced Points: {forcedPoints.Count}\n" +
            $"    Optional Points: {optionalPoints.Count}");
    }

    private static int SpawnFromPointForced(
        EnemyPoint point,
        ExpeditionDefinition def,
        EnemyManager enemyManager)
    {
        int capacityHere = point.allowMultipleEnemies ? Mathf.Max(1, point.maxEnemies) : 1;

        int num = point.forceMax
            ? capacityHere
            : point.allowMultipleEnemies && capacityHere > 1
                ? Random.Range(1, capacityHere + 1)
                : 1;

        return SpawnEnemiesAtPoint(point, def, enemyManager, num);
    }

    private static int SpawnFromPointRandom(
        EnemyPoint point,
        ExpeditionDefinition def,
        int remaining,
        EnemyManager enemyManager)
    {
        if (remaining <= 0)
            return 0;

        int capacityHere = point.allowMultipleEnemies ? Mathf.Max(1, point.maxEnemies) : 1;

        int num;

        if (point.forceMax)
        {
            num = Mathf.Min(capacityHere, remaining);
        }
        else if (point.allowMultipleEnemies && capacityHere > 1)
        {
            int maxPossible = Mathf.Min(capacityHere, remaining);
            num = Random.Range(1, maxPossible + 1);
        }
        else
        {
            num = 1;
        }

        return SpawnEnemiesAtPoint(point, def, enemyManager, num);
    }

    private static int SpawnEnemiesAtPoint(
        EnemyPoint point,
        ExpeditionDefinition def,
        EnemyManager enemyManager,
        int amount)
    {
        var candidates = GetEnemyCandidatesForPoint(point, def);
        int spawnedHere = 0;

        for (int i = 0; i < amount; i++)
        {
            var enemyDef = PickEnemyDefinitionFromCandidates(candidates);
            if (enemyDef == null)
                continue;

            Vector3 pos = point.transform.position;

            if (amount > 1 && point.spawnRadius > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * point.spawnRadius;
                pos += new Vector3(offset.x, 0f, offset.y);
            }

            enemyManager.SpawnEnemy(enemyDef, pos, Quaternion.identity);
            spawnedHere++;
        }

        return spawnedHere;
    }

    #endregion

    #region Loot

    private static void InitializeLoot(ExpeditionDefinition def, LootManager lootManager)
    {
        if (lootManager == null)
        {
            Debug.LogWarning("RunInit.Loot: No hay LootManager registrado.");
            return;
        }

        int spawned = 0;

        var lootPoints = GameObject.FindObjectsByType<LootPoint>(FindObjectsSortMode.None);

        foreach (var p in lootPoints)
        {
            LootTable table = LootSystem.ResolveLootTable(p, def);
            if (table == null)
            {
                Debug.LogWarning($"RunInit.Loot: LootPoint '{p.name}' no tiene LootTable disponible.");
                continue;
            }

            lootManager.SpawnLoot(table, p.transform, p.prefab);
            spawned++;
        }

#if UNITY_EDITOR
        Debug.Log($"[RunInit.Loot] Spawned loot containers: {spawned}");
#endif
    }

    #endregion

    #region Helpers

    private static List<EnemyDefinition> GetEnemyCandidatesForPoint(
        EnemyPoint point,
        ExpeditionDefinition def)
    {
        if (point.Enemies != null && point.Enemies.Count > 0)
            return point.Enemies;

        return def.defaultAllowedEnemies;
    }

    private static EnemyDefinition PickEnemyDefinitionFromCandidates(
        List<EnemyDefinition> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    #endregion
}