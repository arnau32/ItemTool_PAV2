#if UNITY_EDITOR
using System;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Base común para todos los elementos reutilizables de ToolUI.
    /// Centraliza la inicialización, clases USS comunes y evita duplicar patrones de construcción.
    /// </summary>
    public abstract class ToolUIElement : VisualElement
    {
        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private bool initialized;

        // ─────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────

        /// <summary>
        /// Inicializa el componente una sola vez, añade la clase USS común
        /// y ejecuta la construcción visual interna.
        /// </summary>
        protected void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            AddToClassList("tool-ui-element");
            Build();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la jerarquía visual interna del componente.
        /// Cada componente derivado debe implementar aquí su UI.
        /// </summary>
        protected abstract void Build();

        // ─────────────────────────────────────
        // Factory Helpers
        // ─────────────────────────────────────

        /// <summary>
        /// Crea un Label opcionalmente marcado con una clase USS.
        /// </summary>
        protected Label CreateLabel(string text, string className = null)
        {
            var label = new Label(text ?? string.Empty);

            if (!string.IsNullOrEmpty(className))
                label.AddToClassList(className);

            return label;
        }

        /// <summary>
        /// Crea un Button opcionalmente marcado con una clase USS.
        /// Si no recibe callback, el botón queda deshabilitado.
        /// </summary>
        protected Button CreateButton(string text, Action onClick, string className = null)
        {
            var button = new Button(() => onClick?.Invoke())
            {
                text = text ?? string.Empty
            };

            button.SetEnabled(onClick != null);

            if (!string.IsNullOrEmpty(className))
                button.AddToClassList(className);

            return button;
        }
    }
}
#endif