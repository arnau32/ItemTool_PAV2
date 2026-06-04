#if UNITY_EDITOR
using UnityEngine;

[DisallowMultipleComponent]
public class IdleRoutineTester : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private EnemyBase _enemyBase;
    [SerializeField] private InteractPoint[] _sceneInteractPoints;

    [Header("Spawn test InteractPoint")] [SerializeField]
    private bool _spawnDebugPoint = true;

    [SerializeField] private Vector3 _debugPointOffset = new Vector3(3f, 0f, 0f);

    private GameObject _debugPointGO;

    private void Awake()
    {
        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    private void Start()
    {
        if (!_spawnDebugPoint) return;

        _debugPointGO = new GameObject("DEBUG_InteractPoint");
        _debugPointGO.transform.position = transform.position + _debugPointOffset;
        _debugPointGO.AddComponent<SphereCollider>().radius = 0.3f;
        _debugPointGO.AddComponent<InteractPoint>();
        Debug.Log($"[IdleRoutineTester] Spawned debug InteractPoint at {_debugPointGO.transform.position}");
    }

    private void OnDestroy()
    {
        if (_debugPointGO != null)
            Destroy(_debugPointGO);
    }

    private void OnGUI()
    {
        if (_enemyBase == null) return;

        var ctx = _enemyBase.Context;
        if (ctx == null) return;

        var perception = ctx.Perception;
        var profile = ctx.BehaviorProfile;

        var sm = GetStateMachine();
        string stateName = sm?.CurrentState?.GetType().Name ?? "—";

        string routineInfo = "—";
        string phaseInfo = "—";
        string timerInfo = "—";

        var currentState = sm?.CurrentState;

        if (currentState is PatrollState patroll)
        {
            phaseInfo = patroll.DebugPhase;
            routineInfo = patroll.DebugRoutine;
            timerInfo = $"{patroll.DebugRoutineTimer:F1} / {patroll.DebugRoutineDuration:F1}s";
        }
        else if (currentState is IdleState idle)
        {
            phaseInfo = "Idle";
            routineInfo = idle.DebugRoutine;
            timerInfo = $"{idle.DebugRoutineTimer:F1} / {idle.DebugRoutineDuration:F1}s";
        }

        var r = new Rect(10, 10, 360, 240);
        GUI.Box(r, GUIContent.none);

        GUILayout.BeginArea(r);
        GUILayout.Label($"  <b>{_enemyBase.name}</b>");
        GUILayout.Label($"  State      : {stateName}");
        GUILayout.Label($"  Phase      : {phaseInfo}");
        GUILayout.Label($"  Routine    : {routineInfo}");
        GUILayout.Label($"  Timer      : {timerInfo}");
        GUILayout.Space(4);
        GUILayout.Label($"  AlertLevel : {perception.AlertLevel}  ({perception.AlertValue:F2})");
        GUILayout.Label($"  HasVisual  : {perception.HasVisual}");
        GUILayout.Label($"  HasAudio   : {perception.HasAudio}");
        GUILayout.Label($"  LastKnown  : {perception.LastKnownPosition}");
        GUILayout.Space(4);
        GUILayout.Label($"  Routines   : {(profile.idleRoutines?.Length > 0 ? profile.idleRoutines.Length.ToString() : "none")}");

        if (_sceneInteractPoints != null && _sceneInteractPoints.Length > 0)
        {
            int available = 0;
            foreach (var p in _sceneInteractPoints)
                if (p != null && p.IsAvailable)
                    available++;
            GUILayout.Label($"  InteractPts: {available}/{_sceneInteractPoints.Length} available");
        }

        GUILayout.EndArea();
    }

    private StateMachine GetStateMachine()
    {
        if (_enemyBase == null) return null;

        return typeof(CharacterBase)
            .GetField("_stateMachine",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
            ?.GetValue(_enemyBase) as StateMachine;
    }
}
#endif