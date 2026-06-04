#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Layout reutilizable para herramientas tipo browser.
    /// Incluye toolbar superior, sidebar izquierda fija y panel derecho scrollable.
    /// </summary>
    public class ToolBrowserLayout : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public ToolToolbar Toolbar { get; private set; }
        public ToolFixedSidebarLayout SplitLayout { get; private set; }

        public VisualElement Sidebar => SplitLayout.Sidebar;

        public ScrollView DetailsScroll { get; private set; }
        public VisualElement DetailsRoot { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly float sidebarWidth;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolBrowserLayout(float sidebarWidth = 360f)
        {
            this.sidebarWidth = Mathf.Max(ToolUISizes.SidebarMinWidth, sidebarWidth);
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la estructura principal del browser.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1;
            style.minHeight = 0;

            BuildToolbar();
            BuildContent();
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la toolbar superior.
        /// </summary>
        private void BuildToolbar()
        {
            Toolbar = new ToolToolbar();
            Add(Toolbar);
        }

        /// <summary>
        /// Construye el área principal con sidebar y panel de detalles.
        /// </summary>
        private void BuildContent()
        {
            SplitLayout = new ToolFixedSidebarLayout(sidebarWidth);
            SplitLayout.style.flexGrow = 1;

            DetailsScroll = new ScrollView(ScrollViewMode.Vertical);
            DetailsScroll.style.flexGrow = 1;
            DetailsScroll.style.minHeight = 0;
            DetailsScroll.style.paddingLeft = ToolUISizes.LargeGap;
            DetailsScroll.style.paddingRight = ToolUISizes.LargeGap;
            DetailsScroll.style.paddingTop = ToolUISizes.Gap;

            DetailsRoot = new VisualElement();
            DetailsRoot.style.flexDirection = FlexDirection.Column;
            DetailsRoot.style.flexGrow = 1;
            DetailsRoot.style.minWidth = 0;

            DetailsScroll.Add(DetailsRoot);
            SplitLayout.Content.Add(DetailsScroll);

            Add(SplitLayout);
        }
    }
}
#endif