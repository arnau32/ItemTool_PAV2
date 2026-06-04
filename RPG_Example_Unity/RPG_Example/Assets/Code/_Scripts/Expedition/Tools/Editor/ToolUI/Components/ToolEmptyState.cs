#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Estado vacío reutilizable.
    /// Se usa cuando no hay selección o contenido disponible.
    /// </summary>
    public class ToolEmptyState : ToolUIElement
    {
        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly string message;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolEmptyState(string message)
        {
            this.message = message;
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el estado vacío centrado.
        /// </summary>
        protected override void Build()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;

            var label = new Label(message ?? string.Empty);

            label.style.opacity = 0.6f;
            label.style.fontSize = 13;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            Add(label);
        }
    }
}
#endif