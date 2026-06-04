using UnityEngine;
using UnityEngine.Pool;

public abstract class BaseObjectPool<T> : MonoBehaviour where T : Component, IPoolable
{
    [Header("Pool Settings")]
    [SerializeField] protected T prefab;
    [SerializeField] protected int defaultCapacity = 10;
    [SerializeField] protected int maxPoolSize = 20;
    [SerializeField] protected bool collectionCheck = true;

    private int _activeCount;
    protected IObjectPool<T> objectPool;

    public int CountActive => _activeCount;
    public int CountInactive => objectPool != null ? objectPool.CountInactive : 0;
    public int CountAll => CountActive + CountInactive;

    // Optional Singleton pattern for specific Pools
    public static BaseObjectPool<T> Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void InitializePool()
    {
        objectPool = new ObjectPool<T>(
            createFunc: CreatePooledObject,
            actionOnGet: OnTakeFromPool,
            actionOnRelease: OnReturnToPool,
            actionOnDestroy: OnDestroyPoolObject,
            collectionCheck: collectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxPoolSize
        );
    }

    protected virtual T CreatePooledObject()
    {
        var obj = Instantiate(prefab, transform);
        obj.gameObject.SetActive(false);

        // Ensure a clean initial state.
        obj.OnReturnToPool();

        return obj;
    }

    protected virtual void OnTakeFromPool(T obj)
    {
        obj.gameObject.SetActive(true);
        obj.OnSpawnFromPool();
    }

    protected virtual void OnReturnToPool(T obj)
    {
        obj.OnReturnToPool();
        obj.gameObject.SetActive(false);
        obj.transform.SetParent(transform, false);
    }

    protected virtual void OnDestroyPoolObject(T obj)
    {
        if (obj != null) Destroy(obj.gameObject);
    }

    #region Public Methods

    public T Get()
    {
        _activeCount++;
        return objectPool.Get();
    }

    public T Get(Vector3 position, Quaternion rotation)
    {
        var obj = Get();
        obj.transform.SetPositionAndRotation(position, rotation);
        return obj;
    }

    public void Release(T obj)
    {
        if (obj == null) return;

        objectPool.Release(obj);
        _activeCount = Mathf.Max(0, _activeCount - 1);
    }

    #endregion
}
