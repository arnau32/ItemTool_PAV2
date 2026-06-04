#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolUI
{
    /// <summary>
    /// Fila reutilizable para listas de elementos.
    /// Mantiene un diseño compacto horizontal:
    /// icono, título flexible, subtítulo alineado a la derecha e indicador de estado.
    /// </summary>
    public class ToolListRow : ToolUIElement
    {
        // ─────────────────────────────────────
        // Public API
        // ─────────────────────────────────────

        public Image Icon { get; private set; }
        public Label Title { get; private set; }
        public Label Subtitle { get; private set; }
        public Label Indicator { get; private set; }

        // ─────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────

        public ToolListRow()
        {
            Initialize();
        }

        // ─────────────────────────────────────
        // Build
        // ─────────────────────────────────────

        /// <summary>
        /// Construye la row compacta horizontal.
        /// </summary>
        protected override void Build()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = ToolUISizes.ListRowHeight;
            style.paddingLeft = ToolUISizes.Gap;
            style.paddingRight = ToolUISizes.Gap;

            Icon = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };

            Icon.style.width = ToolUISizes.SmallIcon;
            Icon.style.height = ToolUISizes.SmallIcon;
            Icon.style.minWidth = ToolUISizes.SmallIcon;
            Icon.style.marginRight = ToolUISizes.LargeGap;

            Title = new Label();
            Title.style.flexGrow = 1;
            Title.style.flexShrink = 1;
            Title.style.overflow = Overflow.Hidden;
            Title.style.unityFontStyleAndWeight = FontStyle.Bold;

            Subtitle = new Label();
            Subtitle.style.minWidth = 90;
            Subtitle.style.marginLeft = ToolUISizes.Gap;
            Subtitle.style.unityTextAlign = TextAnchor.MiddleRight;
            Subtitle.style.opacity = 0.7f;
            Subtitle.style.overflow = Overflow.Hidden;

            Indicator = new Label();
            Indicator.style.width = 16;
            Indicator.style.minWidth = 16;
            Indicator.style.marginLeft = ToolUISizes.Gap;
            Indicator.style.fontSize = 12;
            Indicator.style.unityTextAlign = TextAnchor.MiddleCenter;
            Indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            Indicator.style.display = DisplayStyle.None;

            Add(Icon);
            Add(Title);
            Add(Subtitle);
            Add(Indicator);
        }

        // ─────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────

        /// <summary>
        /// Configura el icono de la fila.
        /// </summary>
        public void SetIcon(Texture texture)
        {
            Icon.image = texture;
            Icon.style.opacity = texture != null ? 1f : 0.25f;
        }

        /// <summary>
        /// Configura el título y subtítulo de la fila.
        /// El subtítulo se mantiene a la derecha, respetando el diseño compacto original.
        /// </summary>
        public void SetText(string title, string subtitle = null)
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
        /// Muestra un indicador visual de estado.
        /// </summary>
        public void SetIndicator(string text, Color color, string tooltip = null)
        {
            Indicator.text = text ?? string.Empty;
            Indicator.tooltip = tooltip ?? string.Empty;

            Indicator.style.color = color;
            Indicator.style.opacity = 1f;
            Indicator.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Oculta el indicador y limpia su estado visual para evitar residuos al reciclar filas.
        /// </summary>
        /// <summary>
        /// Oculta visualmente el indicador manteniendo su espacio reservado
        /// para evitar saltos de layout en listas reciclables.
        /// </summary>
        public void HideIndicator()
        {
            Indicator.text = string.Empty;
            Indicator.tooltip = string.Empty;

            Indicator.style.color = StyleKeyword.Null;
            Indicator.style.opacity = 0f;

            Indicator.style.display = DisplayStyle.Flex;
        }
    }
}
#endif