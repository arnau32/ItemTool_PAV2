using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParryData))]
public class ParryDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Parry Data", EditorStyles.boldLabel);

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerAnimation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crossFade"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("staminaCost"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Tuning", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inputBufferSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldownSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("failLockoutSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("successLockoutSeconds"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Poise Damage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("poiseDamage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("perfectPoiseDamage"));

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Open Parry Window Editor", GUILayout.Height(30)))
        {
            ParryWindowEditor.Open((ParryData)target);
        }

        serializedObject.ApplyModifiedProperties();
    }
}