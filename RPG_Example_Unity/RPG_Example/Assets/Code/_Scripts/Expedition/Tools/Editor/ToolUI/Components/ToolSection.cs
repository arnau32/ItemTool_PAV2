#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Foldout reutilizable para dividir secciones dentro de paneles de edición.
    /// Mantiene un contenedor interno para separar el header del contenido.
    /// </summary>
    public class ToolSection : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Foldout Foldout { get; private set; }
        public VisualElement Content { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly string title;
        private readonly bool defaultOpen;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolSection(string title, bool defaultOpen = true)
        {
            this.title = title;
            this.defaultOpen = defaultOpen;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el foldout y su contenedor interno de contenido.
        /// </summary>
        protected override void Build()
        {
            Foldout = new Foldout
            {
                text = title ?? string.Empty,
                value = defaultOpen
            };

            Foldout.style.marginTop = ToolUISizes.Gap;
            Foldout.style.marginBottom = ToolUISizes.Gap;

            Content = new VisualElement();
            Content.style.flexDirection = FlexDirection.Column;

            Foldout.Add(Content);
            Add(Foldout);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Limpia solamente el contenido interno, manteniendo el foldout y su estado.
        /// </summary>
        public void ClearContent()
        {
            Content?.Clear();
        }
    }
}
#endif