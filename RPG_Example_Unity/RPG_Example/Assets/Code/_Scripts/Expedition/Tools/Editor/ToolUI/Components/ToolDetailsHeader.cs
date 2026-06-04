#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Header reutilizable para paneles de detalles.
    /// Incluye icono, título, subtítulo y botones de acción.
    /// </summary>
    public class ToolDetailsHeader : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Image Icon { get; private set; }
        public Label Title { get; private set; }
        public Label Subtitle { get; private set; }
        public VisualElement ActionsContainer { get; private set; }

        // ─────────────────────────────────────
        // Fields
        // ─────────────────────────────────────

        private readonly Action onSelect;
        private readonly Action onCopy;

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolDetailsHeader(Action onSelect = null, Action onCopy = null)
        {
            this.onSelect = onSelect;
            this.onCopy = onCopy;

            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la estructura visual base del header.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingTop = ToolUISizes.LargeGap;
            style.paddingBottom = ToolUISizes.LargeGap;

            Icon = BuildIcon();
            Add(Icon);

            Add(BuildTextColumn());

            ActionsContainer = BuildActionsContainer();
            Add(ActionsContainer);

            if (onSelect != null)
                AddActionButton("Select", onSelect);

            if (onCopy != null)
                AddActionButton("Copy", onCopy);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Configura título y subtítulo.
        /// </summary>
        public void SetTexts(string title, string subtitle = null)
        {
            Title.text = title ?? string.Empty;
            Title.tooltip = title ?? string.Empty;

            Subtitle.text = subtitle ?? string.Empty;
            Subtitle.tooltip = subtitle ?? string.Empty;

            Subtitle.style.display = string.IsNullOrWhiteSpace(subtitle)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        /// <summary>
        /// Configura el icono mostrado en el header.
        /// </summary>
        public void SetIcon(Texture texture)
        {
            Icon.image = texture;
            Icon.style.opacity = texture != null ? 1f : 0.25f;
        }

        /// <summary>
        /// Añade un botón pequeño al área de acciones.
        /// </summary>
        public Button AddActionButton(string text, Action onClick)
        {
            var button = new Button(() => onClick?.Invoke())
            {
                text = text ?? string.Empty
            };

            button.style.height = ToolUISizes.SmallButtonHeight;
            button.style.marginLeft = ToolUISizes.Gap;

            button.SetEnabled(onClick != null);

            ActionsContainer.Add(button);
            return button;
        }

        // ─────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Construye el icono principal.
        /// </summary>
        private static Image BuildIcon()
        {
            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };

            image.style.width = ToolUISizes.HeaderIcon;
            image.style.height = ToolUISizes.HeaderIcon;
            image.style.marginRight = ToolUISizes.LargeGap;

            return image;
        }

        /// <summary>
        /// Construye la columna de texto principal.
        /// </summary>
        private VisualElement BuildTextColumn()
        {
            var column = new VisualElement();

            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;

            Title = new Label();
            Title.style.fontSize = 16;
            Title.style.unityFontStyleAndWeight = FontStyle.Bold;

            Subtitle = new Label();
            Subtitle.style.opacity = 0.7f;
            Subtitle.style.marginTop = 2;

            column.Add(Title);
            column.Add(Subtitle);

            return column;
        }

        /// <summary>
        /// Construye el contenedor de botones de acción.
        /// </summary>
        private static VisualElement BuildActionsContainer()
        {
            var container = new VisualElement();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            return container;
        }

        /// <summary>
        /// Adapta el layout cuando el ancho es reducido.
        /// </summary>
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            bool small = evt.newRect.width < 520f;

            style.flexDirection = small ? FlexDirection.Column : FlexDirection.Row;

            ActionsContainer.style.alignSelf = small
                ? Align.FlexStart
                : Align.Auto;

            ActionsContainer.style.marginTop = small
                ? ToolUISizes.Gap
                : 0;
        }
    }
}
#endif