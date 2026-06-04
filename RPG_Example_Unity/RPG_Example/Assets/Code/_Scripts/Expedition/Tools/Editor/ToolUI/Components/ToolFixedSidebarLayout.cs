#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Layout reutilizable con sidebar izquierda de ancho fijo y panel derecho flexible.
    /// Sustituye a TwoPaneSplitView cuando no queremos que el usuario pueda redimensionar la lista.
    /// Ideal para tools tipo browser: lista izquierda + detalles derecha.
    /// </summary>
    public class ToolFixedSidebarLayout : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public VisualElement Sidebar { get; private set; }
        public VisualElement Content { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly float sidebarWidth;
        private readonly bool drawSeparator;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolFixedSidebarLayout(float sidebarWidth = 360f, bool drawSeparator = true)
        {
            this.sidebarWidth = Mathf.Max(ToolUISizes.SidebarMinWidth, sidebarWidth);
            this.drawSeparator = drawSeparator;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el layout horizontal con sidebar fija y contenido flexible.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.flexGrow = 1;
            style.minHeight = 0;

            Sidebar = BuildSidebar();
            Content = BuildContent();

            Add(Sidebar);
            Add(Content);
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el panel lateral fijo.
        /// </summary>
        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();

            sidebar.style.flexDirection = FlexDirection.Column;
            sidebar.style.width = sidebarWidth;
            sidebar.style.minWidth = sidebarWidth;
            sidebar.style.maxWidth = sidebarWidth;
            sidebar.style.flexGrow = 0;
            sidebar.style.flexShrink = 0;
            sidebar.style.minHeight = 0;

            if (drawSeparator)
            {
                sidebar.style.borderRightWidth = 1;
                sidebar.style.borderRightColor = new Color(0f, 0f, 0f, 0.25f);
            }

            return sidebar;
        }

        /// <summary>
        /// Construye el panel derecho flexible.
        /// </summary>
        private static VisualElement BuildContent()
        {
            var content = new VisualElement();

            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;
            content.style.flexShrink = 1;
            content.style.minWidth = 0;
            content.style.minHeight = 0;

            return content;
        }
    }
}
#endif