
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(WindowEvent))]
public class WindowEventDrawer : PropertyDrawer
{
     public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // Label + bar + slider + fields
        return line * 4f + spacing * 5f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
            var startProp = property.FindPropertyRelative("start");
        var endProp   = property.FindPropertyRelative("end");

        float start = startProp.floatValue;
        float end   = endProp.floatValue;

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        float y = position.y;
        float fullW = position.width;
        float x = position.x;

        // -----------------------------
        // 1) LABEL
        // -----------------------------
        Rect labelRect = new Rect(x, y, fullW, line);
        EditorGUI.PrefixLabel(labelRect, label);
        y += line + spacing;

        // -----------------------------
        // 2) BAR (red / green)
        // -----------------------------
        Rect barRect = new Rect(x + 4f, y, fullW - 8f, line * 0.8f);

        EditorGUI.DrawRect(barRect, new Color(0.8f, 0.1f, 0.1f)); // red

        float gx = barRect.x + start * barRect.width;
        float gWidth = Mathf.Max(0, (end - start) * barRect.width);
        Rect greenRect = new Rect(gx, barRect.y, gWidth, barRect.height);

        EditorGUI.DrawRect(greenRect, new Color(0.1f, 0.7f, 0.1f)); // green

        y += barRect.height + spacing;

        // -----------------------------
        // 3) SLIDER (MinMax)
        // -----------------------------
        Rect sliderRect = new Rect(x + 4f, y, fullW - 8f, line);
        EditorGUI.BeginChangeCheck();
        EditorGUI.MinMaxSlider(sliderRect, ref start, ref end, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (end < start) end = start;

            startProp.floatValue = start;
            endProp.floatValue   = end;
        }

        y += line + spacing;

        // -----------------------------
        // 4) NUMERIC FIELDS
        // -----------------------------
        float halfW = (fullW - 8f) * 0.5f - 2f;

        Rect startRect = new Rect(x + 4f, y, halfW, line);
        Rect endRect   = new Rect(x + 4f + halfW + 4f, y, halfW, line);

        EditorGUI.BeginChangeCheck();
        start = EditorGUI.FloatField(startRect, "Start", start);
        end   = EditorGUI.FloatField(endRect,   "End", end);
        if (EditorGUI.EndChangeCheck())
        {
            start = Mathf.Clamp01(start);
            end   = Mathf.Clamp01(end);
            if (end < start) end = start;

            startProp.floatValue = start;
            endProp.floatValue   = end;
        }
    }
}
