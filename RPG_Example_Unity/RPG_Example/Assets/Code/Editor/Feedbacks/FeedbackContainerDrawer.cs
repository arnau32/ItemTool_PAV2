#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FeedbacksNagu
{
    [CustomPropertyDrawer(typeof(FeedbackContainer))]
    public class FeedbackContainerDrawer : PropertyDrawer
    {
        private static readonly float LineH = EditorGUIUtility.singleLineHeight;
        private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;
        private const float ButtonW = 52f;
        private const float CountBadgeW = 24f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var feedbacksProp = property.FindPropertyRelative("feedbacks");
            int count = feedbacksProp != null ? feedbacksProp.arraySize : 0;

            // Header row: label | count badge | [Edit] button
            var labelRect = new Rect(position.x, position.y, position.width - ButtonW - CountBadgeW - 4f, LineH);
            var badgeRect = new Rect(labelRect.xMax + 2f, position.y, CountBadgeW, LineH);
            var buttonRect = new Rect(badgeRect.xMax + 2f, position.y, ButtonW, LineH);

            // Foldout label with type summary as tooltip
            string tooltip = BuildTooltip(feedbacksProp);
            EditorGUI.LabelField(labelRect, new GUIContent(label.text, tooltip), EditorStyles.boldLabel);

            // Count badge
            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = count > 0 ? new Color(0.4f, 0.9f, 0.4f) : Color.gray }
            };
            EditorGUI.LabelField(badgeRect, count.ToString(), badgeStyle);

            // Edit button — opens the EditorWindow bound to this property
            if (GUI.Button(buttonRect, "Edit", EditorStyles.miniButton))
            {
                FeedbackContainerEditorWindow.Open(property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return LineH;
        }

        private static string BuildTooltip(SerializedProperty feedbacksProp)
        {
            if (feedbacksProp == null || feedbacksProp.arraySize == 0)
                return "Empty — click Edit to add feedbacks.";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < feedbacksProp.arraySize; i++)
            {
                var elem = feedbacksProp.GetArrayElementAtIndex(i);
                if (elem == null) continue;

                var typeName = elem.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(typeName)) continue;

                // Extract just the class name from "Assembly TypeName"
                int lastDot = typeName.LastIndexOf('.');
                int space = typeName.IndexOf(' ');
                string name = lastDot >= 0
                    ? typeName.Substring(lastDot + 1)
                    : (space >= 0 ? typeName.Substring(space + 1) : typeName);

                if (i > 0) sb.Append('\n');
                sb.Append("• ").Append(ObjectNames.NicifyVariableName(name));
            }

            return sb.ToString();
        }
    }
}
#endif