using UnityEngine;

public class CheatCode : MonoBehaviour
{
    #region Fields

    [Header("Teleport")]
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Transform[] _teleportPoints = new Transform[5];

    [Header("Activators")]
    [SerializeField] private GameObject[] _activationTargets = new GameObject[3];

    private static readonly KeyCode[] TELEPORT_KEYS =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5
    };

    private static readonly KeyCode[] ACTIVATION_KEYS =
    {
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8
    };

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        HandleTeleport();
        HandleActivation();
    }

    #endregion

    #region Private Methods

    private void HandleTeleport()
    {
        for (int i = 0; i < TELEPORT_KEYS.Length; i++)
        {
            if (!Input.GetKeyDown(TELEPORT_KEYS[i])) continue;
            if (_teleportPoints[i] == null) return;
            if (_playerMovement == null) return;

            _playerMovement.Warp(_teleportPoints[i].position);
            return;
        }
    }

    private void HandleActivation()
    {
        for (int i = 0; i < ACTIVATION_KEYS.Length; i++)
        {
            if (!Input.GetKeyDown(ACTIVATION_KEYS[i])) continue;
            if (_activationTargets[i] == null) return;

            _activationTargets[i].SetActive(true);
            return;
        }
    }

    #endregion
}
