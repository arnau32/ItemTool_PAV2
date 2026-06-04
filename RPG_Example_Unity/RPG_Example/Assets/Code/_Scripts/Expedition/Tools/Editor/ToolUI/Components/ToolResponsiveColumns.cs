#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Contenedor reutilizable de dos columnas.
    /// Cambia automáticamente a layout vertical cuando el ancho disponible baja del breakpoint.
    /// </summary>
    public class ToolResponsiveColumns : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public VisualElement Left { get; private set; }
        public VisualElement Right { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly float breakpoint;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolResponsiveColumns(float breakpoint = ToolUISizes.DetailsResponsiveBreakpoint)
        {
            this.breakpoint = breakpoint;
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye las dos columnas y registra la adaptación responsive.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.FlexStart;

            Left = CreateColumn();
            Left.style.flexGrow = 1;
            Left.style.flexShrink = 1;
            Left.style.marginRight = ToolUISizes.ColumnGap;

            Right = CreateColumn();
            Right.style.flexShrink = 1;
            Right.style.minWidth = ToolUISizes.SidebarMinWidth;

            Add(Left);
            Add(Right);

            RegisterCallback<GeometryChangedEvent>(evt => ApplyResponsiveLayout(evt.newRect.width));
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Crea una columna vertical base.
        /// </summary>
        private static VisualElement CreateColumn()
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            return column;
        }

        /// <summary>
        /// Aplica layout horizontal o vertical según el ancho disponible.
        /// </summary>
        private void ApplyResponsiveLayout(float width)
        {
            bool small = width < breakpoint;

            style.flexDirection = small ? FlexDirection.Column : FlexDirection.Row;
            Left.style.marginRight = small ? 0 : ToolUISizes.ColumnGap;
            Right.style.marginTop = small ? ToolUISizes.LargeGap : 0;
        }
    }
}
#endif