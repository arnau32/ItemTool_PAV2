using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterStat))]
public class CharacterStatsDrawer : PropertyDrawer
{
    const float Padding = 2f;
    const float Space = 6f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Expected fields en CharacterStat:
        // - Enums.StatType statTypeAffected;
        // - float baseValue;   Change it if different below or wont wok
        var statProp = property.FindPropertyRelative("statTypeAffected");

        var baseValueProp = property.FindPropertyRelative("baseValue");
        if (baseValueProp == null) baseValueProp = property.FindPropertyRelative("_baseValue");
        if (baseValueProp == null) baseValueProp = property.FindPropertyRelative("BaseValue"); // fallback habitual

        position.height = EditorGUIUtility.singleLineHeight;
        position.y += Padding;

        // Balance rect: 60% enum, 40% float
        float enumWidth = Mathf.Round(position.width * 0.6f);
        float floatWidth = position.width - enumWidth - Space;

        var enumRect  = new Rect(position.x, position.y, enumWidth, position.height);
        var valueRect = new Rect(enumRect.xMax + Space, position.y, floatWidth, position.height);

        // Drawing
        if (statProp != null)
            EditorGUI.PropertyField(enumRect, statProp, GUIContent.none, true);
        else
            EditorGUI.LabelField(enumRect, "statTypeAffected no encontrado");

        if (baseValueProp != null)
            EditorGUI.PropertyField(valueRect, baseValueProp, GUIContent.none, true);
        else
            EditorGUI.LabelField(valueRect, "baseValue no encontrado");

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight + Padding * 2f;
    }
}
