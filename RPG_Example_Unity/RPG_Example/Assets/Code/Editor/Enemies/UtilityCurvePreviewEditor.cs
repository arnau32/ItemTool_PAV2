using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UtilityConsideration))]
public class UtilityCurvePreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Curve Preview", EditorStyles.boldLabel);

        UtilityConsideration c = (UtilityConsideration)target;

        Rect r = GUILayoutUtility.GetRect(200, 100);
        EditorGUI.DrawRect(r, Color.black);

        Handles.color = Color.green;

        for (int i = 0; i < 100; i++)
        {
            var t0 = i / 100f;
            var t1 = (i + 1) / 100f;

            var v0 = c.EvaluateDebug(t0);
            var v1 = c.EvaluateDebug(t1);

            Vector3 p0 = new Vector3(r.x + r.width * t0, r.y + r.height * (1f - v0), 0);
            Vector3 p1 = new Vector3(r.x + r.width * t1, r.y + r.height * (1f - v1), 0);

            Handles.DrawLine(p0, p1);
        }
    }
}
