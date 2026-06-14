#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class ExpeditionBrowserWindow
{
    private sealed class Domain
    {
        private readonly ExpeditionBrowserWindow _w;

        public Domain(ExpeditionBrowserWindow w)
        {
            _w = w;
        }

        public void LoadPrefs()
        {
            if (_w.SearchField != null)
                _w.SearchField.SetValueWithoutNotify(EditorPrefs.GetString(PrefSearch, string.Empty));

            int savedTab = EditorPrefs.GetInt(PrefActiveTab, (int)ExpeditionListTab.Enemies);
            if (!Enum.IsDefined(typeof(ExpeditionListTab), savedTab))
                savedTab = (int)ExpeditionListTab.Enemies;

            _w.CurrentTab = (ExpeditionListTab)savedTab;
            _w.IncludeInactive = EditorPrefs.GetBool(PrefIncludeInactive, true);

            _w.IncludeInactiveToggle?.SetValueWithoutNotify(_w.IncludeInactive);
            _w._ui.UpdateTabToggles();
        }

        public void SavePrefs()
        {
            if (_w.SearchField != null)
                EditorPrefs.SetString(PrefSearch, _w.SearchField.value ?? string.Empty);

            EditorPrefs.SetInt(PrefActiveTab, (int)_w.CurrentTab);
            EditorPrefs.SetBool(PrefIncludeInactive, _w.IncludeInactive);
        }

        public void SetActiveTab(ExpeditionListTab tab)
        {
            if (_w.CurrentTab == tab)
                return;

            _w.CurrentTab = tab;
            _w.SelectedRecord = null;

            _w._ui.UpdateTabToggles();
            SavePrefs();

            _w.RequestRefreshDebounced(RefreshFlags.Filter | RefreshFlags.List | RefreshFlags.Details);
        }

        public void SetIncludeInactive(bool value)
        {
            _w.IncludeInactive = value;
            SavePrefs();
            _w.Refresh(RefreshFlags.Hard);
        }

        public void ScanScene()
        {
            _w.AllRecords.Clear();

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return;

            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root == null) continue;

                var enemyComponents = root.GetComponentsInChildren<EnemyBase>(_w.IncludeInactive);
                foreach (var enemy in enemyComponents)
                    _w.AllRecords.Add(BuildEnemyRecord(enemy));

                var enemyPoints = root.GetComponentsInChildren<EnemyPoint>(_w.IncludeInactive);
                foreach (var point in enemyPoints)
                    _w.AllRecords.Add(BuildEnemyPointRecord(point));

                var lootPoints = root.GetComponentsInChildren<LootPoint>(_w.IncludeInactive);
                foreach (var point in lootPoints)
                    _w.AllRecords.Add(BuildLootPointRecord(point));
            }

            _w.AllRecords.Sort((a, b) =>
            {
                var kindCompare = a.Kind.CompareTo(b.Kind);
                if (kindCompare != 0) return kindCompare;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            if (_w.SelectedRecord != null && !_w.AllRecords.Contains(_w.SelectedRecord))
                _w.SelectedRecord = null;
        }

        public void Refilter()
        {
            _w.FilteredRecords.Clear();

            IEnumerable<SceneEntityRecord> query = _w.AllRecords;

            query = _w.CurrentTab switch
            {
                ExpeditionListTab.Enemies => query.Where(r =>
                    r.Kind == SceneEntityKind.Enemy ||
                    r.Kind == SceneEntityKind.EnemyPoint),

                ExpeditionListTab.LootPoints => query.Where(r =>
                    r.Kind == SceneEntityKind.LootPoint),

                ExpeditionListTab.Map => Enumerable.Empty<SceneEntityRecord>(),

                _ => Enumerable.Empty<SceneEntityRecord>()
            };

            var search = (_w.SearchField?.value ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(r =>
                    (r.Name ?? string.Empty).ToLowerInvariant().Contains(s) ||
                    (r.HierarchyPath ?? string.Empty).ToLowerInvariant().Contains(s));
            }

            _w.FilteredRecords.AddRange(query);

            if (_w.SelectedRecord != null && !_w.FilteredRecords.Contains(_w.SelectedRecord))
                _w.SelectedRecord = null;
        }

        public void RebuildValidations()
        {
            _w.SeverityCache.Clear();
            _w.ValidationCache.Clear();

            foreach (var record in _w.AllRecords)
            {
                var messages = Validate(record);
                _w.ValidationCache[record] = messages;
                _w.SeverityCache[record] = messages.Count == 0
                    ? ValidationSeverity.None
                    : messages.Max(m => m.Severity);
            }
        }

        public void SelectRecord(SceneEntityRecord record)
        {
            _w.SelectedRecord = record;
            _w._ui.RefreshDetails();
            _w._ui.RefreshList();
        }

        public void SelectInEditor(SceneEntityRecord record)
        {
            if (record?.GameObject == null) return;
            Selection.activeObject = record.GameObject;
        }

        public void Ping(SceneEntityRecord record)
        {
            if (record?.GameObject == null) return;
            EditorGUIUtility.PingObject(record.GameObject);
        }

        public void Frame(SceneEntityRecord record)
        {
            if (record?.GameObject == null) return;

            Selection.activeObject = record.GameObject;
            EditorGUIUtility.PingObject(record.GameObject);

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        public List<ValidationMessage> GetMessages(SceneEntityRecord record)
        {
            if (record == null) return new List<ValidationMessage>();
            return _w.ValidationCache.TryGetValue(record, out var msgs) ? msgs : Validate(record);
        }

        private static SceneEntityRecord BuildEnemyRecord(EnemyBase enemy)
        {
            var go = enemy != null ? enemy.gameObject : null;

            return new SceneEntityRecord
            {
                GameObject = go,
                Component = enemy,
                Kind = SceneEntityKind.Enemy,
                Name = go != null ? go.name : "<null>",
                HierarchyPath = BuildHierarchyPath(go != null ? go.transform : null)
            };
        }

        private static SceneEntityRecord BuildEnemyPointRecord(EnemyPoint point)
        {
            var go = point != null ? point.gameObject : null;

            return new SceneEntityRecord
            {
                GameObject = go,
                Component = point,
                Kind = SceneEntityKind.EnemyPoint,
                Name = go != null ? go.name : "<null>",
                HierarchyPath = BuildHierarchyPath(go != null ? go.transform : null)
            };
        }

        private static SceneEntityRecord BuildLootPointRecord(LootPoint point)
        {
            var go = point != null ? point.gameObject : null;

            return new SceneEntityRecord
            {
                GameObject = go,
                Component = point,
                Kind = SceneEntityKind.LootPoint,
                Name = go != null ? go.name : "<null>",
                HierarchyPath = BuildHierarchyPath(go != null ? go.transform : null)
            };
        }

        private static string BuildHierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;

            var parts = new Stack<string>();
            while (t != null)
            {
                parts.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", parts);
        }

        private static List<ValidationMessage> Validate(SceneEntityRecord record)
        {
            var messages = new List<ValidationMessage>();

            if (record == null || record.GameObject == null || record.Component == null)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Entidad inválida o destruida."
                });

                return messages;
            }

            if (!record.GameObject.activeInHierarchy)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Info,
                    Message = "El GameObject está inactivo en jerarquía."
                });
            }

            switch (record.Kind)
            {
                case SceneEntityKind.Enemy:
                    ValidateEnemy(record, messages);
                    break;

                case SceneEntityKind.EnemyPoint:
                    ValidateEnemyPoint(record, messages);
                    break;

                case SceneEntityKind.LootPoint:
                    ValidateLootPoint(record, messages);
                    break;
            }

            return messages;
        }

        private static void ValidateEnemy(SceneEntityRecord record, List<ValidationMessage> messages)
        {
            var enemy = record.Component as EnemyBase;
            var go = record.GameObject;
            var stats = go != null ? go.GetComponent<CharacterStats>() : null;

            if (stats == null)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Falta CharacterStats en el mismo GameObject."
                });
            }

            if (enemy == null)
                return;

            var so = new SerializedObject(enemy);

            if (so.FindProperty("_definition")?.objectReferenceValue == null)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "EnemyBase no tiene _definition asignada."
                });
            }
        }

        private static void ValidateEnemyPoint(SceneEntityRecord record, List<ValidationMessage> messages)
        {
            var point = record.Component as EnemyPoint;
            if (point == null) return;

            if (point.Enemies == null || point.Enemies.Count == 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "EnemyPoint no tiene EnemyDefinitions asignados. Usará los default de ExpeditionDefinition."
                });
            }

            if (point.allowMultipleEnemies && point.maxEnemies < 1)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "allowMultipleEnemies está activo pero maxEnemies < 1."
                });
            }

            if (point.spawnRadius < 0f)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    Message = "spawnRadius no puede ser negativo."
                });
            }
        }

        private static void ValidateLootPoint(SceneEntityRecord record, List<ValidationMessage> messages)
        {
            var point = record.Component as LootPoint;
            if (point == null) return;

            if (point.LootTables == null || point.LootTables.Count == 0)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "LootPoint no tiene LootTables asignadas. Usará las default de ExpeditionDefinition."
                });
            }

            if (point.prefab == null)
            {
                messages.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "LootPoint no tiene prefab de Storage asignado."
                });
            }
        }
    }
}
#endif