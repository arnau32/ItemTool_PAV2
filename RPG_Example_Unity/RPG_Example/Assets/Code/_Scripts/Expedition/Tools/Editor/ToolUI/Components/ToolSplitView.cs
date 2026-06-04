#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Wrapper reutilizable para TwoPaneSplitView.
    /// Simplifica la creación de layouts divididos horizontales.
    /// </summary>
    public class ToolSplitView : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public TwoPaneSplitView SplitView { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly VisualElement leftPanel;
        private readonly VisualElement rightPanel;
        private readonly int fixedPaneIndex;
        private readonly float fixedPaneSize;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolSplitView(
            VisualElement leftPanel,
            VisualElement rightPanel,
            float fixedPaneSize,
            int fixedPaneIndex = 0)
        {
            this.leftPanel = leftPanel;
            this.rightPanel = rightPanel;
            this.fixedPaneSize = fixedPaneSize;
            this.fixedPaneIndex = fixedPaneIndex;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el split view y añade ambos paneles.
        /// </summary>
        protected override void Build()
        {
            style.flexGrow = 1;

            SplitView = new TwoPaneSplitView(
                fixedPaneIndex,
                fixedPaneSize,
                TwoPaneSplitViewOrientation.Horizontal);

            SplitView.style.flexGrow = 1;

            SplitView.Add(leftPanel ?? CreateFallbackPanel());
            SplitView.Add(rightPanel ?? CreateFallbackPanel());

            Add(SplitView);
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Crea un panel vacío de fallback para evitar null references.
        /// </summary>
        private static VisualElement CreateFallbackPanel()
        {
            var ve = new VisualElement();
            ve.style.flexGrow = 1;
            return ve;
        }
    }
}
#endif