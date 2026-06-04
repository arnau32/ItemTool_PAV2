using UnityEngine;
using UnityEditor;

namespace FeedbacksNagu
{
    // Custom PropertyDrawer for FeedbackPlayAudio.
    // Shows: active toggle, sound reference, and an editor-only Preview button.
    // NOTE: FeedbackPlayRandomAudio has its own drawer (FeedbackPlayRandomAudioDrawer).
    [CustomPropertyDrawer(typeof(FeedbackPlayAudio))]
    public class FeedbackPlayAudioDrawer : PropertyDrawer
    {
        // Cached heights to avoid recalculating every frame
        private static readonly float LineH   = EditorGUIUtility.singleLineHeight;
        private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;

        // Total rows when expanded: foldout + active + sound + preview button = 4
        private const int ExpandedRowCount = 4;

        // ─── Drawing ────────────────────────────────────────────────────────────

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Foldout header
            var foldoutRect = new Rect(position.x, position.y, position.width, LineH);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + LineH + Spacing;

                // "active" field — maps directly to FeedbackBase.active
                DrawPropertyField(ref y, position, property, "active");

                // "sound" field — maps directly to FeedbackPlayAudio.sound (EventReference)
                DrawPropertyField(ref y, position, property, "sound");

                // Preview button — calls FeedbackPlayAudio.PreviewPlay() via managed reference.
                // Only available in editor; the method is wrapped in #if UNITY_EDITOR in the class.
                var btnRect = new Rect(position.x, y, position.width, LineH);
                if (GUI.Button(btnRect, "Preview Sound"))
                    TriggerPreview(property);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // ─── Height ─────────────────────────────────────────────────────────────

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return LineH;

            // (LineH + Spacing) * N — uniform rows, no variable-height arrays involved
            return (LineH + Spacing) * ExpandedRowCount;
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        // Draws a named child property and advances y by one row.
        private static void DrawPropertyField(ref float y, Rect position,
                                              SerializedProperty parent, string fieldName)
        {
            var child = parent.FindPropertyRelative(fieldName);

            // Guard: field must exist — if null, the class and drawer are out of sync.
            if (child == null)
            {
                Debug.LogWarning($"[FeedbackPlayAudioDrawer] Field '{fieldName}' not found. " +
                                 "Make sure the drawer matches the current FeedbackPlayAudio class.");
                return;
            }

            var rect = new Rect(position.x, y, position.width, LineH);
            EditorGUI.PropertyField(rect, child);
            y += LineH + Spacing;
        }

        // Retrieves the actual FeedbackPlayAudio instance from the managed reference
        // and calls PreviewPlay() without reflection — fully type-safe.
        private static void TriggerPreview(SerializedProperty property)
        {
            // managedReferenceValue gives us the boxed object stored via [SerializeReference]
            if (property.managedReferenceValue is FeedbackPlayAudio feedback)
                feedback.PreviewPlay();
            else
                Debug.LogWarning("[FeedbackPlayAudioDrawer] Could not resolve FeedbackPlayAudio instance for preview.");
        }
    }
}