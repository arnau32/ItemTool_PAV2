using UnityEngine;

public class CinematicWalker : MonoBehaviour
{
    #region Fields

    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private bool _loop = false;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private bool _faceDirection = true;
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private float _waypointTolerance = 0.3f;

    private int _currentIndex;
    private bool _isWalking;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        if (_playOnStart)
            StartWalking();
    }

    private void Update()
    {
        if (!_isWalking || _waypoints == null || _waypoints.Length == 0) return;

        Transform target = _waypoints[_currentIndex];
        if (target == null) return;

        Vector3 targetPos = new Vector3(target.position.x, target.position.y, target.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, _speed * Time.deltaTime);

        if (_faceDirection)
        {
            Vector3 dir = targetPos - transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            }
        }

        if (Vector3.Distance(transform.position, targetPos) < _waypointTolerance)
            AdvanceWaypoint();
    }

    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null) continue;
            Gizmos.DrawSphere(_waypoints[i].position, 0.15f);
            if (i < _waypoints.Length - 1 && _waypoints[i + 1] != null)
                Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
        }

        if (_waypoints[0] != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _waypoints[0].position);
        }
    }

    #endregion

    #region Public API

    public void StartWalking()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;
        _currentIndex = 0;
        _isWalking = true;
    }

    public void StopWalking()
    {
        _isWalking = false;
    }

    public void ResumeWalking()
    {
        _isWalking = true;
    }

    #endregion

    #region Private

    private void AdvanceWaypoint()
    {
        _currentIndex++;
        
        if (_currentIndex < _waypoints.Length) return;

        if (_loop)
        {
            _currentIndex = 0;
        }
        else
        {
            _isWalking = false;
        }
    }

    #endregion
}
