using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Collections.Generic;

[CustomEditor(typeof(ItemData), true)]
public class ItemDataEditor : Editor
{
    private ReorderableList _blockList;
    private SerializedProperty _descriptionBlocksProp;

    private static readonly string[] _blockTypeLabels = { "Text Block", "Stat Block" };

    private void OnEnable()
    {
        _descriptionBlocksProp = serializedObject.FindProperty("descriptionBlocks");
        BuildReorderableList();
    }

    private void BuildReorderableList()
    {
        _blockList = new ReorderableList(serializedObject, _descriptionBlocksProp,
            draggable: true, displayHeader: true,
            displayAddButton: false, displayRemoveButton: true);

        _blockList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Description Blocks", EditorStyles.boldLabel);
        };

        _blockList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = _descriptionBlocksProp.GetArrayElementAtIndex(index);
            if (element.managedReferenceValue == null)
            {
                EditorGUI.LabelField(rect, $"[{index}] (null)");
                return;
            }

            string typeName = element.managedReferenceValue.GetType().Name;
            float lineH = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;

            var headerRect = new Rect(rect.x, rect.y + pad, rect.width, lineH);
            EditorGUI.LabelField(headerRect, $"[{index}] {typeName}", EditorStyles.miniBoldLabel);

            float y = rect.y + lineH + pad * 2;
            var child = element.Copy();
            var end = element.GetEndProperty();
            bool entered = child.NextVisible(true);

            while (entered && !SerializedProperty.EqualContents(child, end))
            {
                float height = EditorGUI.GetPropertyHeight(child, true);
                var propRect = new Rect(rect.x + 12, y, rect.width - 12, height);
                EditorGUI.PropertyField(propRect, child, true);
                y += height + pad;
                entered = child.NextVisible(false);
            }
        };

        _blockList.elementHeightCallback = index =>
        {
            var element = _descriptionBlocksProp.GetArrayElementAtIndex(index);
            if (element.managedReferenceValue == null)
                return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;

            float lineH = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;
            float total = lineH + pad * 2; // header row

            var child = element.Copy();
            var end = element.GetEndProperty();
            bool entered = child.NextVisible(true);

            while (entered && !SerializedProperty.EqualContents(child, end))
            {
                total += EditorGUI.GetPropertyHeight(child, true) + pad;
                entered = child.NextVisible(false);
            }

            return total + pad;
        };

        _blockList.onRemoveCallback = list =>
        {
            _descriptionBlocksProp.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all default fields except descriptionBlocks
        DrawPropertiesExcluding(serializedObject, "descriptionBlocks");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Item Description Blocks", EditorStyles.boldLabel);

        _blockList.DoLayoutList();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Text Block"))
        {
            int idx = _descriptionBlocksProp.arraySize;
            _descriptionBlocksProp.InsertArrayElementAtIndex(idx);
            _descriptionBlocksProp.GetArrayElementAtIndex(idx).managedReferenceValue = new TextDescriptionBlock();
        }
        if (GUILayout.Button("+ Add Stat Block"))
        {
            int idx = _descriptionBlocksProp.arraySize;
            _descriptionBlocksProp.InsertArrayElementAtIndex(idx);
            _descriptionBlocksProp.GetArrayElementAtIndex(idx).managedReferenceValue = new StatDescriptionBlock();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
