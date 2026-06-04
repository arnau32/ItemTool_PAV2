using UnityEditor;
using UnityEngine;

namespace Code.Editor
{
    [CustomEditor(typeof(CharacterStats))]
    public class CharacterStatsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                Object scriptObj = null;
                if (target is MonoBehaviour mb)
                    scriptObj = MonoScript.FromMonoBehaviour(mb);
                else if (target is ScriptableObject so)
                    scriptObj = MonoScript.FromScriptableObject(so);

                EditorGUILayout.ObjectField("Script", scriptObj, typeof(MonoScript), false);
            }

            var templateProp = serializedObject.FindProperty("_baseStatTemplate");
            EditorGUILayout.PropertyField(templateProp, new GUIContent("Base Stat Template"));

            bool hasTemplate = templateProp.objectReferenceValue != null;

            if (hasTemplate)
            {
                EditorGUILayout.HelpBox("Stats loaded from ScriptableObject. Base Stats list is ignored at runtime.", MessageType.Info);
            }

            using (new EditorGUI.DisabledGroupScope(hasTemplate))
            {
                var baseStatsProp = serializedObject.FindProperty("_baseStats");
                EditorGUILayout.PropertyField(baseStatsProp, new GUIContent("Base Stats (Fallback)"), includeChildren: true);

                if (baseStatsProp != null && baseStatsProp.isArray && !hasTemplate)
                {
                    var seen = new System.Collections.Generic.HashSet<int>();
                    bool hasDupes = false;
                    for (int i = 0; i < baseStatsProp.arraySize; i++)
                    {
                        var el = baseStatsProp.GetArrayElementAtIndex(i);
                        var statType = el.FindPropertyRelative("statTypeAffected");
                        if (statType != null)
                        {
                            int enumIndex = statType.enumValueIndex;
                            if (!seen.Add(enumIndex)) hasDupes = true;
                        }
                    }
                    if (hasDupes)
                        EditorGUILayout.HelpBox("There are duplicated stats. Verify it is intentional.", MessageType.Info);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
