#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ComboAttackAction))]
public class ComboAttackActionEditor : EnemyActionEditor
{
    private ReorderableList _hitsList;
    private SerializedProperty _attackData;
    private SerializedProperty _additionalHits;
    private SerializedProperty _gapBetweenHits;
    private SerializedProperty _comboChance;

    protected override void OnEnable()
    {
        base.OnEnable();

        _attackData = serializedObject.FindProperty("attackData");
        _additionalHits = serializedObject.FindProperty("additionalHits");
        _gapBetweenHits = serializedObject.FindProperty("gapBetweenHits");
        _comboChance = serializedObject.FindProperty("comboChance");

        _hitsList = new ReorderableList(serializedObject, _additionalHits, true, true, true, true);

        _hitsList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Additional Hits (Hit 2, 3…)");

        _hitsList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            float btnW = 100f;
            float gap = 4f;
            var fieldR = new Rect(rect.x, rect.y, rect.width - btnW - gap, rect.height);
            var btnR = new Rect(fieldR.xMax + gap, rect.y, btnW, rect.height);

            var elem = _additionalHits.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(fieldR, elem, new GUIContent($"Hit {index + 2}"));

            var data = elem.objectReferenceValue as AttackData;
            using (new EditorGUI.DisabledScope(data == null))
            {
                if (GUI.Button(btnR, "Edit Windows"))
                    AttackWindowEditor.Open(data);
            }
        };

        _hitsList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Combo Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_attackData, new GUIContent("Hit 1 (first)"));
        var firstData = _attackData.objectReferenceValue as AttackData;
        using (new EditorGUI.DisabledScope(firstData == null))
        {
            if (GUILayout.Button("Edit Windows", GUILayout.Width(100),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                AttackWindowEditor.Open(firstData);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(_gapBetweenHits);
        EditorGUILayout.PropertyField(_comboChance);

        EditorGUILayout.Space(4);
        _hitsList.DoLayoutList();

        EditorGUILayout.Space(8);

        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "considerations",
            "attackData",
            "additionalHits",
            "gapBetweenHits",
            "comboChance");

        EditorGUILayout.Space(8);

        DrawConsiderationsList();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif