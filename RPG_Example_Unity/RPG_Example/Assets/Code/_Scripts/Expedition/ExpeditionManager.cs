using UnityEngine;

public class ExpeditionManager : MonoBehaviour, IGameServices
{
    public static ExpeditionManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private ExpeditionDefinition expeditionDefinition;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int debugSeed = 12345;

    public ExpeditionDefinition Definition => expeditionDefinition;
    public int Seed { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Multiple ExpeditionManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (useRandomSeed)
        {
            Seed = Random.Range(int.MinValue, int.MaxValue);
        }
        else
        {
            Seed = debugSeed;
        }

        RunInitializationSystem.InitializeRun(this, expeditionDefinition, Seed);
    }
}