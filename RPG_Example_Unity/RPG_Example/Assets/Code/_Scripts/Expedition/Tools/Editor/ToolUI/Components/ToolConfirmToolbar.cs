#if UNITY_EDITOR
using System;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Barra reutilizable de acciones de mantenimiento.
    /// Incluye botones típicos como Add, Sort y Clear.
    /// </summary>
    public class ToolConfirmToolbar : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Button AddButton { get; private set; }
        public Button SortButton { get; private set; }
        public Button ClearButton { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly string addText;
        private readonly string sortText;
        private readonly string clearText;

        private readonly Action onAdd;
        private readonly Action onSort;
        private readonly Action onClear;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolConfirmToolbar(
            Action onAdd = null,
            Action onSort = null,
            Action onClear = null,
            string addText = "+ Add",
            string sortText = "Sort",
            string clearText = "Clear")
        {
            this.onAdd = onAdd;
            this.onSort = onSort;
            this.onClear = onClear;

            this.addText = addText;
            this.sortText = sortText;
            this.clearText = clearText;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la barra horizontal y sus botones.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = ToolUISizes.Gap;

            AddButton = CreateSmallButton(addText, onAdd);
            SortButton = CreateSmallButton(sortText, onSort);
            ClearButton = CreateSmallButton(clearText, onClear);

            Add(AddButton);
            Add(SortButton);
            Add(ClearButton);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Muestra u oculta el botón de ordenación.
        /// </summary>
        public void SetSortVisible(bool visible)
        {
            if (SortButton == null)
                return;

            SortButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Muestra u oculta el botón de limpieza.
        /// </summary>
        public void SetClearVisible(bool visible)
        {
            if (ClearButton == null)
                return;

            ClearButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Crea un botón pequeño de toolbar.
        /// Si no recibe callback, queda deshabilitado.
        /// </summary>
        private static Button CreateSmallButton(string text, Action onClick)
        {
            var button = new Button(() => onClick?.Invoke())
            {
                text = text ?? string.Empty
            };

            button.style.height = ToolUISizes.SmallButtonHeight;
            button.style.marginRight = ToolUISizes.Gap;

            button.SetEnabled(onClick != null);

            return button;
        }
    }
}
#endif