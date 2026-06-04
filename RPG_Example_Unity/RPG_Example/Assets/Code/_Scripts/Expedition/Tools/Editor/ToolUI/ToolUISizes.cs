#if UNITY_EDITOR
namespace ToolUI
{
    /// <summary>
    /// Constantes visuales compartidas por los componentes ToolUI.
    /// Evita números mágicos repetidos y mantiene una escala visual consistente.
    /// </summary>
    public static class ToolUISizes
    {
        // ─────────────────────────────────────
        // Spacing
        // ─────────────────────────────────────

        public const float SmallGap = 4f;
        public const float Gap = 6f;
        public const float LargeGap = 8f;
        public const float ColumnGap = 14f;

        // ─────────────────────────────────────
        // Controls
        // ─────────────────────────────────────

        public const float SmallButtonHeight = 20f;

        // ─────────────────────────────────────
        // Lists
        // ─────────────────────────────────────

        public const float ListRowHeight = 28f;

        // ─────────────────────────────────────
        // Icons
        // ─────────────────────────────────────

        public const float SmallIcon = 20f;
        public const float HeaderIcon = 64f;
        public const float PreviewIcon = 96f;

        // ─────────────────────────────────────
        // Layout
        // ─────────────────────────────────────

        public const float LabelWidth = 140f;
        public const float DetailsResponsiveBreakpoint = 620f;
        public const float SidebarMinWidth = 120f;
    }
}
#endif