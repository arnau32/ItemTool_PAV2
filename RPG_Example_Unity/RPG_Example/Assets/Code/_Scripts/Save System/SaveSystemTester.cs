using UnityEngine;

/// <summary>
/// Manual test harness for the SaveSystem.
/// Attach to any GameObject in the Base scene, run in Play Mode.
/// Remove before shipping.
///
/// HOW TO USE:
///   1. Enter Play Mode in Base scene.
///   2. Use the buttons in the Inspector (or keyboard shortcuts below) to
///      trigger save/load operations and inspect results in the Console.
///   3. Press F5 to save, F9 to load, F6 to delete the save file, F7 to print state.
/// </summary>
public class SaveSystemTester : MonoBehaviour
{
    [Header("References (auto-found if left empty)")] [SerializeField]
    private CharacterHealthSystem _health;

    [SerializeField] private StaminaSystem _stamina;

    [Header("Test values")] [SerializeField]
    private float _testDamageAmount = 30f;

    [SerializeField] private float _testHealAmount = 10f;

    private SaveService _save;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Start()
    {
        _save = GameServices.Get<SaveService>();

        if (_health == null)
            _health = FindFirstObjectByType<CharacterHealthSystem>();

        if (_stamina == null)
            _stamina = FindFirstObjectByType<StaminaSystem>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) ForceSave();
        if (Input.GetKeyDown(KeyCode.F9)) ForceLoad();
        if (Input.GetKeyDown(KeyCode.F6)) DeleteSaveFile();
        if (Input.GetKeyDown(KeyCode.F7)) PrintCurrentSaveState();
        if (Input.GetKeyDown(KeyCode.F1)) DealTestDamage();
        if (Input.GetKeyDown(KeyCode.F2)) HealTest();
        if (Input.GetKeyDown(KeyCode.F3)) TakeLevelSnapshot();
        if (Input.GetKeyDown(KeyCode.F4)) SimulateDeath();
    }

    // ── Inspector buttons ─────────────────────────────────────────────────────

    [ContextMenu("① Force Save")]
    public void ForceSave()
    {
        _save.Save();
        Debug.Log("[SaveTester] ✅ Save triggered. Check: " + GetSaveFilePath());
    }

    [ContextMenu("② Force Load + Apply")]
    public void ForceLoad()
    {
        _save.Load();
        _save.ApplyAll();
        Debug.Log("[SaveTester] ✅ Load + ApplyAll triggered.");
        PrintCurrentSaveState();
    }

    [ContextMenu("③ Print current SaveData state")]
    public void PrintCurrentSaveState()
    {
        var data = _save.CurrentSave;
        if (data == null)
        {
            Debug.LogWarning("[SaveTester] No save data in memory.");
            return;
        }

        Debug.Log(
            $"[SaveTester] ── SaveData snapshot ──────────────────\n" +
            $"  meta.onboardingCompleted : {data.meta.onboardingCompleted}\n" +
            $"  meta.lastScene           : {data.meta.lastScene}\n" +
            $"  player.currentHealth     : {data.player.currentHealth}\n" +
            $"  player.currentStamina    : {data.player.currentStamina}\n" +
            $"  inventory stacks         : {data.inventory.inventoryStacks.Count}\n" +
            $"  equipped items           : {data.inventory.equippedItems.Count}\n" +
            $"  consumable slots         : {data.inventory.consumableSlots.Count}\n" +
            $"  hasLevelSnapshot         : {data.inventory.hasLevelSnapshot}\n" +
            $"  quests saved             : {data.quests.entries.Count}\n" +
            $"────────────────────────────────────────────────────"
        );

        // Inventory detail
        for (int i = 0; i < data.inventory.inventoryStacks.Count; i++)
        {
            var s = data.inventory.inventoryStacks[i];
            Debug.Log($"  [inventory {i}] {s.uniqueID} x{s.quantity} @ ({s.gridX},{s.gridY})");
        }

        // Equipment detail
        for (int i = 0; i < data.inventory.equippedItems.Count; i++)
        {
            var e = data.inventory.equippedItems[i];
            Debug.Log($"  [equip] slot={e.slotName}  item={e.uniqueID}");
        }
    }

    [ContextMenu("④ Delete save file (fresh start)")]
    public void DeleteSaveFile()
    {
        string path = GetSaveFilePath();
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            Debug.Log($"[SaveTester] 🗑️ Save file deleted: {path}");
        }
        else
        {
            Debug.Log($"[SaveTester] No save file found at: {path}");
        }
    }

    [ContextMenu("⑤ Deal test damage")]
    public void DealTestDamage()
    {
        if (_health == null)
        {
            Debug.LogWarning("[SaveTester] No health found.");
            return;
        }

        _health.TakeDamage(_testDamageAmount, default);
        Debug.Log($"[SaveTester] 🩸 Dealt {_testDamageAmount} damage. HP01={_health.CurrentHealth01:F2}");
    }

    [ContextMenu("⑥ Heal test")]
    public void HealTest()
    {
        if (_health == null)
        {
            Debug.LogWarning("[SaveTester] No health found.");
            return;
        }

        _health.Heal(_testHealAmount);
        Debug.Log($"[SaveTester] 💊 Healed {_testHealAmount}. HP01={_health.CurrentHealth01:F2}");
    }

    [ContextMenu("⑦ Take Level entry snapshot")]
    public void TakeLevelSnapshot()
    {
        _save.TakeLevelEntrySnapshot();
        Debug.Log("[SaveTester] 📸 Level snapshot taken.");
        PrintCurrentSaveState();
    }

    [ContextMenu("⑧ Simulate death (applies penalty, no scene change)")]
    public void SimulateDeath()
    {
        _save.ApplyDeathPenalty();
        Debug.Log($"[SaveTester] 💀 Death penalty applied ({_save.DeathPenalty}). Inspect inventory to verify.");
        PrintCurrentSaveState();
    }

    [ContextMenu("⑨ Set onboardingCompleted = true + Save")]
    public void MarkOnboardingComplete()
    {
        _save.CurrentSave.meta.onboardingCompleted = true;
        _save.Save();
        Debug.Log("[SaveTester] ✅ onboardingCompleted = true saved.");
    }

    [ContextMenu("⑩ Reset onboardingCompleted = false + Save")]
    public void ResetOnboarding()
    {
        _save.CurrentSave.meta.onboardingCompleted = false;
        _save.Save();
        Debug.Log("[SaveTester] 🔄 onboardingCompleted = false saved. Next play will show OnBoarding.");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string GetSaveFilePath() =>
        System.IO.Path.Combine(Application.persistentDataPath, "save.json");
}