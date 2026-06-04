#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Fila reutilizable para envolver campos de edición dentro de formularios.
    /// Estandariza separación, layout y permite añadir cualquier VisualElement como contenido.
    /// </summary>
    public class ToolFieldRow : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public VisualElement Content { get; private set; }

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        /// <summary>
        /// Crea una fila de campo opcionalmente inicializada con un hijo.
        /// </summary>
        public ToolFieldRow(VisualElement child = null, float marginBottom = ToolUISizes.Gap)
        {
            Initialize();

            style.marginBottom = marginBottom;

            if (child != null)
                AddContent(child);
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el contenedor interno de la fila.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Column;

            Content = new VisualElement();
            Content.style.flexDirection = FlexDirection.Column;

            Add(Content);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un elemento visual al contenido de la fila.
        /// </summary>
        public void AddContent(VisualElement element)
        {
            if (element == null)
                return;

            Content.Add(element);
        }

        /// <summary>
        /// Limpia todos los elementos añadidos al contenido de la fila.
        /// </summary>
        public void ClearContent()
        {
            Content?.Clear();
        }
    }
}
#endif