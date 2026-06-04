using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AttackData))]
public class AttackDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Attack Data", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackHitType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("staminaCost"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("damage"));

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Poise", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("poiseDamage"));

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Hyper Armor", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hasHyperArmor"));

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerAnimation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"));

        // NOTE: timeToChangeAnim is edited inside Attack Window Editor
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crossFade"));

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Open Attack Window Editor", GUILayout.Height(30)))
        {
            AttackWindowEditor.Open((AttackData)target);
        }

        serializedObject.ApplyModifiedProperties();
    }
}