using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissingScriptsCleanerWindow : EditorWindow
{
    private class MissingScriptEntry
    {
        public GameObject gameObject;
        public int missingCount;
    }

    private readonly List<MissingScriptEntry> _entries = new();
    private Vector2 _scroll;
    private bool _includeInactive = true;

    [MenuItem("Tools/Cleaning/Missing Scripts Cleaner")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptsCleanerWindow>("Missing Scripts Cleaner");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Missing / Invalid Scripts Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Escanea la escena abierta y encuentra GameObjects con scripts missing/invalid. " +
            "Puedes limpiarlos individualmente o todos de golpe.",
            MessageType.Info);

        EditorGUILayout.Space();

        _includeInactive = EditorGUILayout.Toggle("Include Inactive", _includeInactive);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan Current Scene", GUILayout.Height(30)))
        {
            ScanCurrentScene();
        }

        GUI.enabled = _entries.Count > 0;
        if (GUILayout.Button("Clean All Found", GUILayout.Height(30)))
        {
            CleanAllFound();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Found Objects: {_entries.Count}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (_entries.Count == 0)
        {
            EditorGUILayout.HelpBox("No missing scripts found. Press 'Scan Current Scene' to search.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                DrawEntry(_entries[i], i);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(MissingScriptEntry entry, int index)
    {
        if (entry.gameObject == null)
            return;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            $"{index + 1}. {entry.gameObject.name}",
            EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField(
            $"Missing: {entry.missingCount}",
            GUILayout.Width(80));

        if (GUILayout.Button("Ping", GUILayout.Width(60)))
        {
            EditorGUIUtility.PingObject(entry.gameObject);
            Selection.activeGameObject = entry.gameObject;
        }

        if (GUILayout.Button("Clean", GUILayout.Width(60)))
        {
            CleanSingle(entry);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.ObjectField("GameObject", entry.gameObject, typeof(GameObject), true);
        EditorGUILayout.LabelField("Path", GetHierarchyPath(entry.gameObject));

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void ScanCurrentScene()
    {
        _entries.Clear();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("No valid loaded scene found.");
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            ScanRecursive(root);
        }

        Debug.Log($"Missing Scripts Cleaner: Found {_entries.Count} GameObjects with missing scripts in scene '{scene.name}'.");
    }

    private void ScanRecursive(GameObject go)
    {
        if (go == null)
            return;

        if (_includeInactive || go.activeInHierarchy)
        {
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount > 0)
            {
                _entries.Add(new MissingScriptEntry
                {
                    gameObject = go,
                    missingCount = missingCount
                });
            }
        }

        Transform transform = go.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            ScanRecursive(transform.GetChild(i).gameObject);
        }
    }

    private void CleanSingle(MissingScriptEntry entry)
    {
        if (entry == null || entry.gameObject == null)
            return;

        Undo.RegisterCompleteObjectUndo(entry.gameObject, "Remove Missing Scripts");
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(entry.gameObject);

        if (removed > 0)
        {
            EditorUtility.SetDirty(entry.gameObject);
            Debug.Log($"Removed {removed} missing script(s) from '{entry.gameObject.name}'.");
        }

        ScanCurrentScene();
    }

    private void CleanAllFound()
    {
        if (_entries.Count == 0)
            return;

        int totalRemoved = 0;

        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry == null || entry.gameObject == null)
                continue;

            Undo.RegisterCompleteObjectUndo(entry.gameObject, "Remove Missing Scripts");
            totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(entry.gameObject);
            EditorUtility.SetDirty(entry.gameObject);
        }

        Debug.Log($"Missing Scripts Cleaner: Removed {totalRemoved} missing script(s).");
        ScanCurrentScene();
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return string.Empty;

        string path = go.name;
        Transform current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
