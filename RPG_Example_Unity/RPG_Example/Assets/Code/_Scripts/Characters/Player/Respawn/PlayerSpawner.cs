using UnityEngine;

public class PlayerSpawner : MonoBehaviour, ISaveable
{
    #region Fields

    [SerializeField] private Transform   _player;
    [SerializeField] private SpawnPoint[] spawnPoints;

    [Tooltip("Enable only in scenes that should save and restore the player's last position (e.g. Village). " +
             "Level scenes should leave this off so spawn points are always used.")]
    [SerializeField] private bool _saveLastPosition = false;

    private SaveService _save;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (GameServices.TryGet(out _save) && _saveLastPosition)
            _save.RegisterSaveable(this);
    }

    private void OnDestroy()
    {
        _save?.UnregisterSaveable(this);
    }

    private void Start()
    {
        SpawnPlayer();
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        if (!_saveLastPosition || _player == null) return;

        var pd = data.player;
        pd.lastPosX       = _player.position.x;
        pd.lastPosY       = _player.position.y;
        pd.lastPosZ       = _player.position.z;
        pd.lastYRotation  = _player.eulerAngles.y;
        pd.hasLastPosition = true;
    }

    public void ApplyFromSave(SaveData data) { }

    #endregion

    #region Private

    private void SpawnPlayer()
    {
        if (_player == null)
        {
            Debug.LogWarning("[PlayerSpawner] Player reference is missing.", this);
            return;
        }

        if (_saveLastPosition && RespawnData.UseSavedPosition && _save != null)
        {
            var pd = _save.CurrentSave.player;
            if (pd.hasLastPosition)
            {
                var pos = new Vector3(pd.lastPosX, pd.lastPosY, pd.lastPosZ);
                var rot = Quaternion.Euler(0f, pd.lastYRotation, 0f);
                MovePlayerTo(pos, rot);
                return;
            }
        }

        SpawnAtPoint();
    }

    private void SpawnAtPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[PlayerSpawner] No SpawnPoints assigned.", this);
            return;
        }

        foreach (SpawnPoint sp in spawnPoints)
        {
            if (sp == null) continue;
            if (sp.Index != RespawnData.SpawnPointIndex) continue;

            MovePlayerTo(sp.transform.position, sp.transform.rotation);

            // Player arrived via spawn point (extraction / death / first load).
            // Re-enable saved position so the next close/reopen resumes from Village.
            if (_saveLastPosition)
                RespawnData.UseSavedPosition = true;

            return;
        }

        Debug.LogWarning($"[PlayerSpawner] No SpawnPoint found for index {RespawnData.SpawnPointIndex}", this);
    }

    private void MovePlayerTo(Vector3 position, Quaternion rotation)
    {
        if (_player.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position        = position;
            rb.rotation        = rotation;
        }

        _player.SetPositionAndRotation(position, rotation);
    }

    #endregion
}
