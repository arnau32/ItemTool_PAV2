#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Fila reutilizable con etiqueta a la izquierda y contenido a la derecha.
    /// Útil para formularios compactos o campos personalizados que no usan label propio.
    /// </summary>
    public class ToolLabeledRow : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Label Label { get; private set; }
        public VisualElement Content { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly string labelText;
        private readonly VisualElement initialContent;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolLabeledRow(string labelText, VisualElement content = null)
        {
            this.labelText = labelText;
            initialContent = content;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la fila horizontal con label fija y contenido flexible.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = ToolUISizes.Gap;

            Label = BuildLabel();
            Content = BuildContent();

            if (initialContent != null)
                AddContent(initialContent);

            Add(Label);
            Add(Content);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un elemento visual al área de contenido.
        /// </summary>
        public void AddContent(VisualElement element)
        {
            if (element == null)
                return;

            Content.Add(element);
        }

        /// <summary>
        /// Limpia el área de contenido manteniendo la label.
        /// </summary>
        public void ClearContent()
        {
            Content?.Clear();
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la label izquierda.
        /// </summary>
        private Label BuildLabel()
        {
            var label = new Label(labelText ?? string.Empty);

            label.style.minWidth = ToolUISizes.LabelWidth;
            label.style.marginRight = ToolUISizes.LargeGap;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;

            return label;
        }

        /// <summary>
        /// Construye el contenedor derecho flexible.
        /// </summary>
        private static VisualElement BuildContent()
        {
            var content = new VisualElement();

            content.style.flexGrow = 1;
            content.style.flexShrink = 1;

            return content;
        }
    }
}
#endif