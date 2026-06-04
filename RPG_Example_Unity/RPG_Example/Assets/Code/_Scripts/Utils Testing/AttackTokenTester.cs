#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// Attach anywhere in the scene to monitor tokens, separation and orbit at runtime.
/// Draws Gizmos on each enemy showing the separation sphere and lateral push vector.
[DisallowMultipleComponent]
public class AttackTokenTester : MonoBehaviour
{
    [SerializeField] private List<EnemyBase> _enemies = new();
    [SerializeField] private bool _autoFindEnemies = true;

    [Header("Separation Debug")] [SerializeField]
    private bool _drawSeparationGizmos = true;

    [SerializeField] private bool _logSeparationPush = false;

    private AttackTokenService _tokenService;
    private readonly HashSet<Enums.Faction> _factionsSeen = new();

    private static readonly Collider[] _buf = new Collider[8];

    private void Start()
    {
        GameServices.TryGet<AttackTokenService>(out _tokenService);

        if (_autoFindEnemies)
        {
            var found = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            _enemies.Clear();
            _enemies.AddRange(found);
        }
    }

    private void OnGUI()
    {
        float rowH = 22f;
        var r = new Rect(10, 10, 420, 50 + _enemies.Count * rowH + 60);
        GUI.Box(r, GUIContent.none);
        GUILayout.BeginArea(r);

        GUILayout.Label("  <b>Enemy Multi-Agent Debug</b>");
        GUILayout.Space(2);

        // ── Token section ─────────────────────────────────────────────────
        if (_tokenService == null)
        {
            GUILayout.Label("  <color=red>AttackTokenService NOT FOUND</color>");
        }
        else
        {
            _factionsSeen.Clear();
            foreach (var e in _enemies)
                if (e != null)
                    _factionsSeen.Add(e.Faction);

            foreach (var faction in _factionsSeen)
            {
                int active = _tokenService.ActiveTokens(faction);
                int max = _tokenService.MaxTokens(faction);
                string col = active >= max ? "red" : "lime";
                GUILayout.Label($"  <b>{faction}</b>  tokens: <color={col}>{active} / {max}</color>");
            }
        }

        GUILayout.Space(4);

        // ── Per-enemy rows ─────────────────────────────────────────────────
        for (int i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy == null) continue;

            var ctx = enemy.Context;
            if (ctx == null) continue;

            bool isAttacking = ctx.Animation.IsAttacking;
            string atkColor = isAttacking ? "red" : "white";
            string atkLabel = isAttacking ? "ATK" : "   ";

            // Separation: count nearby allied enemies
            int nearbyCount = 0;
            int overlapCount = 0;
            var profile = ctx.BehaviorProfile;
            if (profile != null && profile.enemyLayer != 0 && profile.separationRadius > 0f)
            {
                overlapCount = Physics.OverlapSphereNonAlloc(
                    enemy.transform.position, profile.separationRadius,
                    _buf, profile.enemyLayer, QueryTriggerInteraction.Collide);

                for (int j = 0; j < overlapCount; j++)
                {
                    if (_buf[j] != null
                        && _buf[j].transform != enemy.transform
                        && _buf[j].CompareTag("Enemy"))
                        nearbyCount++;
                }
            }

            string sepColor = nearbyCount > 0 ? "yellow" : "lime";
            string sepLabel = $"sep:{nearbyCount}";
            string stateLabel = GetStateName(enemy);

            GUILayout.Label(
                $"  <color={atkColor}>{enemy.name}</color>" +
                $"  [{ctx.Perception.AlertLevel}]" +
                $"  <color={atkColor}>{atkLabel}</color>" +
                $"  <color={sepColor}>{sepLabel}</color>" +
                $"  {stateLabel}");

            if (nearbyCount > 0)
            {
                for (int j = 0; j < overlapCount; j++)
                {
                    if (_buf[j] == null) continue;
                    if (!_buf[j].CompareTag("Enemy")) continue;
                    if (_buf[j].transform == enemy.transform) continue;
                    GUILayout.Label($"    <color=yellow>  └ {_buf[j].name}</color>");
                }
            }
        }

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (!_drawSeparationGizmos || !Application.isPlaying) return;

        foreach (var enemy in _enemies)
        {
            if (enemy == null) continue;

            var ctx = enemy.Context;
            var profile = ctx?.BehaviorProfile;
            if (profile == null || profile.enemyLayer == 0 || profile.separationRadius <= 0f) continue;

            Vector3 pos = enemy.transform.position;
            float radius = profile.separationRadius;

            // Draw separation sphere
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.12f);
            Gizmos.DrawWireSphere(pos, radius);

            // Draw lateral push vector
            int count = Physics.OverlapSphereNonAlloc(
                pos, radius, _buf, profile.enemyLayer, QueryTriggerInteraction.Collide);

            if (ctx.Perception.CurrentTarget != null)
            {
                Vector3 toTarget = ctx.Perception.CurrentTarget.position - pos;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > 0.001f)
                {
                    Vector3 chaseDir = toTarget / dist;
                    Vector3 right = new Vector3(-chaseDir.z, 0f, chaseDir.x);

                    float lateralPush = 0f;
                    for (int j = 0; j < count; j++)
                    {
                        var col = _buf[j];
                        if (col == null || col.transform == enemy.transform) continue;
                        if (!col.CompareTag("Enemy")) continue;

                        Vector3 toOther = pos - col.transform.position;
                        toOther.y = 0f;
                        float d = toOther.magnitude;
                        if (d < 0.001f) continue;

                        float overlap = Mathf.Clamp01(1f - d / radius);
                        lateralPush += Vector3.Dot(toOther.normalized, right) * overlap;
                    }

                    if (Mathf.Abs(lateralPush) > 0.01f)
                    {
                        Vector3 pushVec = right * (Mathf.Clamp(lateralPush, -1f, 1f) * radius * 0.5f);
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(pos + Vector3.up * 0.5f, pos + Vector3.up * 0.5f + pushVec);
                        Gizmos.DrawSphere(pos + Vector3.up * 0.5f + pushVec, 0.12f);

                        if (_logSeparationPush)
                            Debug.Log($"[SepDebug] {enemy.name} lateralPush={lateralPush:F2} pushVec={pushVec}");
                    }
                    else
                    {
                        // No push — draw small green dot to confirm detection is working
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(pos + Vector3.up * 0.5f, 0.08f);
                    }
                }
            }
        }
    }

    private static string GetStateName(EnemyBase enemy)
    {
        var sm = typeof(CharacterBase)
            .GetField("_stateMachine",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
            ?.GetValue(enemy) as StateMachine;

        return sm?.CurrentState?.GetType().Name ?? "—";
    }
}
#endif