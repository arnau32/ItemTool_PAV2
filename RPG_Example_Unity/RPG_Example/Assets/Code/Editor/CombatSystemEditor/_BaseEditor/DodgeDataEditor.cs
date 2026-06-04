using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DodgeData))]
public class DodgeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Dodge Data", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crossFade"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"));

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Open Dodge Window Editor", GUILayout.Height(30)))
        {
            DodgeDataWindowEditor.Open((DodgeData)target);
        }

        serializedObject.ApplyModifiedProperties();
    }
}