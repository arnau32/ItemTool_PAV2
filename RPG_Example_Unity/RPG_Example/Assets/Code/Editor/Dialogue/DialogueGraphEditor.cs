#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal custom inspector for DialogueGraph.
/// All editing is now done in DialogueEditorWindow (double-click the asset or use the button).
/// This inspector intentionally shows only a quick-access button — keeping the Project
/// inspector lightweight while the window handles the full graph workflow.
/// </summary>
[CustomEditor(typeof(DialogueGraph))]
public class DialogueGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var graph = (DialogueGraph)target;

        EditorGUILayout.Space(6f);

        // ── Open button ───────────────────────────────────────────────────────
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.50f, 0.85f);

        if (GUILayout.Button("  ◈  Open Dialogue Editor", GUILayout.Height(36f)))
            DialogueEditorWindow.Open(graph);

        GUI.backgroundColor = prev;

        EditorGUILayout.Space(8f);

        // ── Quick summary ─────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterName"),
            new GUIContent("Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterIllustration"),
            new GUIContent("Illustration"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Graph", EditorStyles.boldLabel);

        int nodeCount = graph.nodes?.Count ?? 0;
        EditorGUILayout.LabelField($"Nodes: {nodeCount}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Start: {(string.IsNullOrEmpty(graph.startNodeId) ? "— none —" : graph.startNodeId)}",
            EditorStyles.miniLabel);

        if (serializedObject.ApplyModifiedProperties())
            EditorUtility.SetDirty(graph);
    }
}
#endif