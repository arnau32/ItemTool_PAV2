#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Sección reutilizable para mostrar y bindear propiedades de un SerializedObject.
    /// Combina ToolSection + PropertyField + ToolFieldRow para construir formularios editoriales.
    /// </summary>
    public class ToolSerializedSection : ToolSection
    {
        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly SerializedObject serializedObject;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolSerializedSection(
            string title,
            SerializedObject serializedObject,
            bool defaultOpen = true)
            : base(title, defaultOpen)
        {
            this.serializedObject = serializedObject;
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un PropertyField bindeado a una propiedad del SerializedObject.
        /// Si la propiedad no existe, muestra un warning dentro de la sección.
        /// </summary>
        public PropertyField AddProperty(string propertyName, string label = null)
        {
            if (serializedObject == null)
            {
                AddError("SerializedObject es null.");
                return null;
            }

            var property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                AddWarning($"No se encontró la propiedad '{propertyName}'.");
                return null;
            }

            var field = string.IsNullOrEmpty(label)
                ? new PropertyField(property)
                : new PropertyField(property, label);

            field.BindProperty(property);

            Content.Add(new ToolFieldRow(field));
            return field;
        }

        /// <summary>
        /// Añade un mensaje informativo dentro de la sección.
        /// </summary>
        public void AddInfo(string message)
        {
            AddHelpBox(message, HelpBoxMessageType.Info);
        }

        /// <summary>
        /// Añade un mensaje de advertencia dentro de la sección.
        /// </summary>
        public void AddWarning(string message)
        {
            AddHelpBox(message, HelpBoxMessageType.Warning);
        }

        /// <summary>
        /// Añade un mensaje de error dentro de la sección.
        /// </summary>
        public void AddError(string message)
        {
            AddHelpBox(message, HelpBoxMessageType.Error);
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un HelpBox envuelto en ToolFieldRow.
        /// </summary>
        private void AddHelpBox(string message, HelpBoxMessageType type)
        {
            Content.Add(new ToolFieldRow(new HelpBox(
                message ?? string.Empty,
                type)));
        }
    }
}
#endif