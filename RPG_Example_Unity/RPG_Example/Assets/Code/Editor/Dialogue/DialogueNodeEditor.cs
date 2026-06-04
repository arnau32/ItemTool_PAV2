using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueNode))]
public class DialogueNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty id = serializedObject.FindProperty("id");
        SerializedProperty nodeType = serializedObject.FindProperty("nodeType");

        SerializedProperty localizedText = serializedObject.FindProperty("localizedText");
        SerializedProperty nextNodeId = serializedObject.FindProperty("nextNodeId");

        SerializedProperty options = serializedObject.FindProperty("options");

        EditorGUILayout.PropertyField(id);
        EditorGUILayout.PropertyField(nodeType);

        DialogueNodeType type = (DialogueNodeType)nodeType.enumValueIndex;

        switch (type)
        {
            case DialogueNodeType.Text:
                EditorGUILayout.Space(10);
                EditorGUILayout.PropertyField(localizedText);
                EditorGUILayout.PropertyField(nextNodeId);
                break;

            case DialogueNodeType.Choice:
                EditorGUILayout.Space(10);
                EditorGUILayout.PropertyField(options, true);
                break;

            case DialogueNodeType.End:
                EditorGUILayout.HelpBox("This is an END node.", MessageType.Info);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
