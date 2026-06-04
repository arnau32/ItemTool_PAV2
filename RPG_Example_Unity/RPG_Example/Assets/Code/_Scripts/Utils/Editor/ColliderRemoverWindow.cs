using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ColliderRemoverWindow : EditorWindow
{
    #region Fields

    private struct ColliderEntry
    {
        public Collider Collider;
        public string GameObjectName;
        public string ColliderTypeName;
    }

    private List<ColliderEntry> _colliderEntries = new List<ColliderEntry>();
    private Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
    private Vector2 _scrollPos;
    private bool _canRestore;
    private int _lastDeletedCount;

    #endregion

    #region Menu Entry

    [MenuItem("Custom Tools/Remove Colliders")]
    public static void OpenWindow()
    {
        ColliderRemoverWindow window = GetWindow<ColliderRemoverWindow>("Remove Colliders");
        window.minSize = new Vector2(380f, 480f);
        window.ScanScene();
        window.Show();
    }

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        ScanScene();
    }

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(4f);
        DrawColliderList();
        EditorGUILayout.Space(8f);
        DrawActionButtons();
    }

    #endregion

    #region Drawing

    private void DrawHeader()
    {
        EditorGUILayout.Space(6f);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField("Collider Remover", titleStyle, GUILayout.Height(24f));
        EditorGUILayout.Space(2f);

        Color prevColor = GUI.color;
        GUI.color = _colliderEntries.Count > 0 ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 1f, 0.5f);

        string countLabel = _colliderEntries.Count == 0
            ? "No colliders found in scene"
            : $"{_colliderEntries.Count} collider(s) found in scene";

        EditorGUILayout.LabelField(countLabel, EditorStyles.centeredGreyMiniLabel, GUILayout.Height(18f));
        GUI.color = prevColor;

        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                ScanScene();
            }
        }

        DrawSeparator();
    }

    private void DrawColliderList()
    {
        if (_colliderEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("La escena no contiene ning\u00fan Collider.", MessageType.Info);
            return;
        }

        Dictionary<string, List<ColliderEntry>> grouped = GroupByType();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        foreach (KeyValuePair<string, List<ColliderEntry>> kvp in grouped)
        {
            string typeName = kvp.Key;
            List<ColliderEntry> entries = kvp.Value;

            if (!_foldouts.ContainsKey(typeName))
                _foldouts[typeName] = true;

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold
            };

            _foldouts[typeName] = EditorGUILayout.BeginFoldoutHeaderGroup(
                _foldouts[typeName],
                $"{typeName}  ({entries.Count})",
                foldoutStyle
            );

            if (_foldouts[typeName])
            {
                EditorGUI.indentLevel++;

                foreach (ColliderEntry entry in entries)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isNull = entry.Collider == null;

                        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                        if (isNull)
                        {
                            labelStyle.normal.textColor = Color.gray;
                        }

                        EditorGUILayout.LabelField(
                            isNull ? $"{entry.GameObjectName}  (destroyed)" : entry.GameObjectName,
                            labelStyle
                        );

                        if (!isNull)
                        {
                            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50f)))
                            {
                                Selection.activeGameObject = entry.Collider.gameObject;
                                EditorGUIUtility.PingObject(entry.Collider.gameObject);
                            }
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2f);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawActionButtons()
    {
        DrawSeparator();
        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            Color prevBg = GUI.backgroundColor;

            bool hasColliders = _colliderEntries.Count > 0;
            GUI.backgroundColor = hasColliders ? new Color(1f, 0.35f, 0.35f) : Color.gray;
            GUI.enabled = hasColliders;

            GUIStyle deleteStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            if (GUILayout.Button("Borrar todos los Colliders", deleteStyle, GUILayout.Height(36f)))
            {
                OnClickDelete();
            }

            GUI.enabled = true;
            GUI.backgroundColor = prevBg;
        }

        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _canRestore ? new Color(0.4f, 0.8f, 1f) : Color.gray;
            GUI.enabled = _canRestore;

            GUIStyle restoreStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            string restoreLabel = _canRestore
                ? $"Restaurar ({_lastDeletedCount} collider(s))"
                : "Restaurar (no hay cambios)";

            if (GUILayout.Button(restoreLabel, restoreStyle, GUILayout.Height(36f)))
            {
                OnClickRestore();
            }

            GUI.enabled = true;
            GUI.backgroundColor = prevBg;
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
    }

    #endregion

    #region Scene Scanning

    private void ScanScene()
    {
        _colliderEntries.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders)
            {
                _colliderEntries.Add(new ColliderEntry
                {
                    Collider = col,
                    GameObjectName = col.gameObject.name,
                    ColliderTypeName = col.GetType().Name
                });
            }
        }

        Repaint();
    }

    private Dictionary<string, List<ColliderEntry>> GroupByType()
    {
        Dictionary<string, List<ColliderEntry>> result = new Dictionary<string, List<ColliderEntry>>();

        foreach (ColliderEntry entry in _colliderEntries)
        {
            if (!result.ContainsKey(entry.ColliderTypeName))
                result[entry.ColliderTypeName] = new List<ColliderEntry>();

            result[entry.ColliderTypeName].Add(entry);
        }

        return result;
    }

    #endregion

    #region Actions

    private void OnClickDelete()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Eliminar Colliders",
            $"\u00bfEst\u00e1s seguro de eliminar TODOS los colliders de la escena?\n\n({_colliderEntries.Count} collider(s) ser\u00e1n eliminados)",
            "S\u00ed, eliminar",
            "No, cancelar"
        );

        if (!confirmed) return;

        Undo.SetCurrentGroupName("Remove All Colliders");
        int groupIndex = Undo.GetCurrentGroup();

        int deleted = 0;
        foreach (ColliderEntry entry in _colliderEntries)
        {
            if (entry.Collider == null) continue;
            Undo.DestroyObjectImmediate(entry.Collider);
            deleted++;
        }

        Undo.CollapseUndoOperations(groupIndex);

        _lastDeletedCount = deleted;
        _canRestore = deleted > 0;

        ScanScene();
    }

    private void OnClickRestore()
    {
        Undo.PerformUndo();
        _canRestore = false;
        ScanScene();
    }

    #endregion
}
