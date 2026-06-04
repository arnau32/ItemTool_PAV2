#if UNITY_EDITOR
using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Toolbar reutilizable con zona izquierda y derecha.
    /// Sirve como base para botones de crear, refrescar, ordenar, filtros o búsqueda.
    /// </summary>
    public class ToolToolbar : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public VisualElement Left { get; private set; }
        public VisualElement Right { get; private set; }

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolToolbar()
        {
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye las zonas izquierda y derecha de la toolbar.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.SpaceBetween;
            style.flexGrow = 0;

            Left = CreateSideContainer(grow: true);
            Right = CreateSideContainer(grow: false);

            Add(Left);
            Add(Right);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Añade un botón a la zona izquierda de la toolbar.
        /// </summary>
        public ToolbarButton AddButtonLeft(string text, Action onClick, float width = 70f)
        {
            var button = CreateToolbarButton(text, onClick, width);
            Left.Add(button);
            return button;
        }

        /// <summary>
        /// Añade un botón a la zona derecha de la toolbar.
        /// </summary>
        public ToolbarButton AddButtonRight(string text, Action onClick, float width = 70f)
        {
            var button = CreateToolbarButton(text, onClick, width);
            Right.Add(button);
            return button;
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Crea un contenedor lateral para la toolbar.
        /// </summary>
        private static VisualElement CreateSideContainer(bool grow)
        {
            var container = new VisualElement();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            if (grow)
                container.style.flexGrow = 1;

            return container;
        }

        /// <summary>
        /// Crea un botón de toolbar.
        /// Si no recibe callback, queda deshabilitado.
        /// </summary>
        private static ToolbarButton CreateToolbarButton(string text, Action onClick, float width)
        {
            var button = new ToolbarButton(() => onClick?.Invoke())
            {
                text = text ?? string.Empty
            };

            button.style.width = width;
            button.style.marginRight = ToolUISizes.Gap;

            button.SetEnabled(onClick != null);

            return button;
        }
    }
}
#endif