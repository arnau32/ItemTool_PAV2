using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-side registry that maps string IDs to GameObjects.
/// Place one instance in each scene that needs dialogue-driven object activation.
/// Fill the entries list in the Inspector — no code required on the target objects.
/// </summary>
public class SceneObjectRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public string id;
        public GameObject target;
    }

    public static SceneObjectRegistry Instance { get; private set; }

    [SerializeField] private Entry[] _entries;

    private Dictionary<string, GameObject> _map;

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SceneObjectRegistry] Duplicate instance — destroying extra.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildMap();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Public API

    public bool TryGet(string id, out GameObject obj)
    {
        if (_map != null && _map.TryGetValue(id, out obj))
            return true;

        obj = null;
        return false;
    }

    #endregion

    #region Private

    private void BuildMap()
    {
        _map = new Dictionary<string, GameObject>(_entries?.Length ?? 0);

        if (_entries == null) return;

        for (int i = 0; i < _entries.Length; i++)
        {
            var e = _entries[i];

            if (string.IsNullOrEmpty(e.id))
            {
                Debug.LogWarning($"[SceneObjectRegistry] Entry [{i}] has no id — skipping.", this);
                continue;
            }

            if (e.target == null)
            {
                Debug.LogWarning($"[SceneObjectRegistry] Entry '{e.id}' has no target — skipping.", this);
                continue;
            }

            if (_map.ContainsKey(e.id))
            {
                Debug.LogWarning($"[SceneObjectRegistry] Duplicate id '{e.id}' at index [{i}] — skipping.", this);
                continue;
            }

            _map[e.id] = e.target;
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_entries == null) return;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (string.IsNullOrEmpty(_entries[i].id))
                Debug.LogWarning($"[SceneObjectRegistry] Entry [{i}] has no id.", this);

            if (_entries[i].target == null)
                Debug.LogWarning($"[SceneObjectRegistry] Entry [{i}] ('{_entries[i].id}') has no target.", this);
        }
    }
#endif
}
