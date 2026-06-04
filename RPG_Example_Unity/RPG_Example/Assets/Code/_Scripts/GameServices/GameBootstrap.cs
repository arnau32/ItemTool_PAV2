using UnityEngine;

public static class GameBootstrap
{
    private const string ServicesPrefabPath = "Services";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var prefab = Resources.Load<GameObject>(ServicesPrefabPath);
        var root   = Object.Instantiate(prefab);
        Object.DontDestroyOnLoad(root);

        // Core — always alive across all scenes.
        GameServices.Register(root.GetComponent<SaveService>());
        GameServices.Register(root.GetComponent<CoroutineRunner>());
        GameServices.Register(root.GetComponent<InputService>());
        GameServices.Register(root.GetComponent<AudioService>());
        GameServices.Register(root.GetComponent<CanvasService>());
        GameServices.Register(root.GetComponent<CombatAnalyticsUploader>());
        GameServices.Register(root.GetComponent<CombatAnalyticsService>());
        GameServices.Register(root.GetComponent<OcclusionService>());
        GameServices.Register(root.GetComponent<QuestService>());
        GameServices.Register(root.GetComponent<MorranContractService>());
        GameServices.Register(root.GetComponent<StorageSaveService>());
        GameServices.Register(root.GetComponent<LanguageManager>());
        GameServices.Register(root.GetComponent<SettingsService>());
        GameServices.Register(root.GetComponent<NpcLevelService>());
        GameServices.Register(root.GetComponent<QualityManager>());
        GameServices.Register<ISceneLoader>(root.GetComponent<SceneLoaderService>());
        GameServices.Register(root.GetComponent<InputIconSettings>());

        var vfxGo = new GameObject("Vfx Pool");
        vfxGo.transform.SetParent(root.transform);
        GameServices.Register(vfxGo.AddComponent<VfxPoolService>());

        GameServices.Register(new TimeScaleManager());
        GameServices.Register<IOutlineService>(new OutlineService());

        GameServices.InitializeAll();

        Application.quitting += GameServices.ShutdownAll;

    }
}